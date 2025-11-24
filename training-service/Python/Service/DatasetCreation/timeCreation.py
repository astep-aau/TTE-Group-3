import json
import random
from pathlib import Path
import numpy as np
import sqlite3


def get_edge_time(route, db_connection, embeddings_dict):
    """Compute traversal times for each edge in the route by querying the database.
    If an edge has no traversals, use the nearest embedding fallback; otherwise use final fallback 5.0.
    """
    times = []
    bucketsAvailable = set()

    # Collect all available buckets across the route using batch query
    edge_ids_str = ",".join(str(e) for e in route)
    cursor = db_connection.cursor()
    cursor.execute(f"""
        SELECT DISTINCT traversal_id 
        FROM traversals 
        WHERE node_id IN ({edge_ids_str})
    """)
    
    for row in cursor.fetchall():
        bucketsAvailable.add(int(row[0]))

    if not bucketsAvailable:
        return []

    chosen_bucket = random.choice(sorted(bucketsAvailable))

    for edgeId in route:
        edge = str(edgeId)

        # Query database for this edge's traversals
        cursor.execute("""
            SELECT traversal_id, time_s 
            FROM traversals 
            WHERE node_id = ?
        """, (edgeId,))
        
        rows = cursor.fetchall()
        
        if not rows:
            # No traversals found - use embedding-based fallback
            found_time = None
            try:
                if edge in embeddings_dict:
                    target_vec = np.array(embeddings_dict[edge], dtype=np.float32)

                    # Build arrays of other embeddings
                    other_keys = []
                    other_vecs = []
                    for k, v in embeddings_dict.items():
                        if k == edge:
                            continue
                        other_keys.append(k)
                        other_vecs.append(v)

                    if other_vecs:
                        vecs = np.asarray(other_vecs, dtype=np.float32)
                        diffs = vecs - target_vec
                        dists = np.linalg.norm(diffs, axis=1)
                        order = np.argsort(dists)

                        # Iterate from nearest to farthest until we find traversal data
                        for idx in order:
                            idx_int = int(idx)
                            other_key = other_keys[idx_int]
                            other_edge_id = int(other_key)
                            
                            # Query database for nearest neighbor's traversals
                            cursor.execute("""
                                SELECT traversal_id, time_s 
                                FROM traversals 
                                WHERE node_id = ?
                            """, (other_edge_id,))
                            
                            neighbor_rows = cursor.fetchall()
                            
                            if neighbor_rows:
                                # Find closest bucket to chosen_bucket
                                bucket_map = {int(row[0]): float(row[1]) for row in neighbor_rows}
                                bucket_keys = sorted(bucket_map.keys())
                                closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                                
                                try:
                                    found_time = bucket_map[closest_bucket]
                                except Exception:
                                    found_time = None
                                    
                                if found_time is not None:
                                    break

            except (KeyError, ValueError, TypeError, np.linalg.LinAlgError, sqlite3.Error):
                found_time = None

            if found_time is not None:
                times.append(found_time)
                continue

            # Final fallback
            times.append(5.0)
            continue

        # Normal path: we have traversals for this edge
        # Build a map of traversal_id -> time_s
        bucket_map = {int(row[0]): float(row[1]) for row in rows}
        bucket_keys = sorted(bucket_map.keys())
        closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
        times.append(bucket_map[closest_bucket])

    return times

def EdgeTraversalTime(route, db_connection=None, embedding_cache=None):
    """Calculate traversal times for a route with proper error handling.
    db_connection and embedding_cache must be provided by the caller (typically from PythonController).
    """
    if not route:
        raise ValueError('No route data provided. (Empty Route)')
    
    if db_connection is None or embedding_cache is None:
        raise ValueError('Database connection and embedding cache must be provided')

    try:
        times = get_edge_time(route, db_connection, embedding_cache)
        return times

    except FileNotFoundError as e:
        raise RuntimeError(str(e))
    except ValueError as e:
        raise RuntimeError(str(e))
    except sqlite3.Error as e:
        raise RuntimeError(f"Database error: {str(e)}")
    except Exception as e:
        raise RuntimeError(f"Unexpected error: {str(e)}")