import json
import random
from pathlib import Path
import numpy as np
import sqlite3

# Module-level cache
_db_connection = None
_embedding_cache = None
_traversal_cache = None

def _initialize_caches():
    global _db_connection, _embedding_cache, _traversal_cache

    # Initialize database connection and traversal cache
    if _db_connection is None:
        db_path = Path(__file__).parent.parent / "Data" / "traversals.db"

        if not db_path.is_file():
            raise FileNotFoundError('"traversals.db" does not exist. (Missing Dataset)')

        try:
            _db_connection = sqlite3.connect(db_path)
            cursor = _db_connection.cursor()

            # Verify traversals table exists and is not empty
            cursor.execute("SELECT COUNT(*) FROM traversals;")
            if cursor.fetchone()[0] == 0:
                raise ValueError('"traversals" table is empty. (Empty Dataset)')

            # Load all traversal data and group by node_id (edge)
            cursor.execute("SELECT node_id, traversal_id, time_s FROM traversals ORDER BY node_id, traversal_id;")
            rows = cursor.fetchall()

            if not rows:
                raise ValueError('"traversals" table has no data. (Empty Dataset)')

            _traversal_cache = {}

            # Group traversals by node_id (edge) and traversal_id (bucket)
            for row in rows:
                try:
                    node_id = str(row[0])
                    traversal_id = str(row[1])
                    time_s = float(row[2])

                    # Initialize edge entry if not exists
                    if node_id not in _traversal_cache:
                        _traversal_cache[node_id] = {"traversals": {}}

                    # Add traversal bucket data
                    _traversal_cache[node_id]["traversals"][traversal_id] = {
                        "time to traverse (s)": time_s
                    }

                except (IndexError, ValueError, TypeError) as e:
                    raise ValueError(f"Invalid data format in traversals table: {str(e)}")

            # Verify we have valid data
            if not _traversal_cache:
                raise ValueError('"traversals" table contains no valid data. (Empty Dataset)')

        except sqlite3.Error as e:
            _db_connection = None
            raise RuntimeError(f"Database error: {str(e)}")

    # Initialize embedding cache (unchanged)
    if _embedding_cache is None:
        embedding_path = Path(__file__).parent.parent / "Data" / "edgeEmbeddings.json"

        if not embedding_path.is_file():
            raise FileNotFoundError('"edgeEmbeddings.json" does not exist. (Missing Dataset)')

        try:
            with open(embedding_path, "r") as f:
                _embedding_cache = json.load(f)

            if not _embedding_cache:
                raise ValueError('"edgeEmbeddings.json" is empty or not loaded. (Empty Dataset)')

        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON in edgeEmbeddings.json: {str(e)}")
        except IOError as e:
            raise RuntimeError(f"Error reading edgeEmbeddings.json: {str(e)}")

def get_edge_time(route, traversalData, embeddings_dict):
    """Compute traversal times for each edge in the route based on traversal data.
    If an edge has no traversals, use the nearest embedding fallback; otherwise use final fallback 5.0.
    """
    times = []
    bucketsAvailable = set()

    # Collect all available buckets
    for edgeId in route:
        edge = str(edgeId)
        if edge in traversalData:
            edgeData = traversalData[edge]
            edgeTraversals = edgeData.get("traversals", {})
            bucketKeys = edgeTraversals.keys()
            bucketsAvailable.update(int(k) for k in bucketKeys)

    if not bucketsAvailable:
        return []

    chosen_bucket = random.choice(sorted(bucketsAvailable))

    for edgeId in route:
        edge = str(edgeId)

        # unified retrieval: missing edge or empty traversals -> fallback flow
        traversals = traversalData.get(edge, {}).get("traversals", {})

        if not traversals:
            # Embedding-based fallback (nearest embedding). If missing or fails, use final fallback.
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
                        vecs = np.asarray(other_vecs, dtype=np.float32)  # shape (M, D)
                        diffs = vecs - target_vec
                        dists = np.linalg.norm(diffs, axis=1)
                        order = np.argsort(dists)

                        # Iterate from nearest to farthest until we find traversal data
                        for idx in order:
                            idx_int = int(idx)
                            other_key = other_keys[idx_int]
                            other_travs = traversalData.get(other_key, {}).get("traversals", {})
                            if other_travs:
                                bucket_keys = sorted(int(k) for k in other_travs.keys())
                                closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                                try:
                                    found_time = float(other_travs[str(closest_bucket)]["time to traverse (s)"])
                                except Exception:
                                    found_time = None
                                if found_time is not None:
                                    # Print the chosen nearest edge and its distance
                                    try:
                                        dist_val = float(dists[idx_int])
                                        print(f"Nearest embedding for edge {edge} -> {other_key} (dist={dist_val:.4f})")
                                    except Exception:
                                        print(f"Nearest embedding for edge {edge} -> {other_key}")
                                    break

            except (KeyError, ValueError, TypeError, np.linalg.LinAlgError):
                found_time = None

            if found_time is not None:
                times.append(found_time)
                continue

            # Final fallback
            times.append(5.0)
            continue

        # Normal path: choose closest bucket for this edge
        bucket_keys = sorted(int(k) for k in traversals.keys())
        closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
        times.append(float(traversals[str(closest_bucket)]["time to traverse (s)"]))

    return times

def EdgeTraversalTime(route):
    """Calculate traversal times for a route with proper error handling."""
    if not route:
        raise ValueError('No route data provided. (Empty Route)')

    try:
        _initialize_caches()
        times = get_edge_time(route, _traversal_cache, _embedding_cache)
        return times

    except FileNotFoundError as e:
        raise RuntimeError(str(e))
    except ValueError as e:
        raise RuntimeError(str(e))
    except sqlite3.Error as e:
        raise RuntimeError(f"Database error: {str(e)}")
    except Exception as e:
        raise RuntimeError(f"Unexpected error: {str(e)}")