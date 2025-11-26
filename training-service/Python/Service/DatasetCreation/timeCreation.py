import json
import random
from pathlib import Path
import numpy as np
import sqlite3
import json
import sys
import logging

# Configure logging
logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

# -------------------------
# Helper: open read-only DB connection
# -------------------------
def get_db_connection():
    db_path = Path(__file__).parent.parent / "Data" / "traversals.db"
    if not db_path.is_file():
        raise FileNotFoundError('"traversals.db" does not exist. (Missing Dataset)')

    try:
        conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, check_same_thread=False)

        # Optional: quick integrity check
        cursor = conn.cursor()
        cursor.execute("PRAGMA integrity_check;")
        result = cursor.fetchone()
        cursor.close()
        if result[0] != "ok":
            conn.close()
            raise RuntimeError("Database is corrupted!")

        return conn
    except sqlite3.Error as e:
        raise RuntimeError(f"Database error: {str(e)}")


# -------------------------
# Load embeddings once in memory
# -------------------------
def load_embeddings():
    embedding_path = Path(__file__).parent.parent / "Service" / "Data" / "edgeEmbeddings.json"
    if not embedding_path.is_file():
        raise FileNotFoundError('"edgeEmbeddings.json" does not exist. (Missing Dataset)')

    try:
        with open(embedding_path, "r") as f:
            embeddings = json.load(f)
        if not embeddings:
            raise ValueError('"edgeEmbeddings.json" is empty.')
        return embeddings
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON in edgeEmbeddings.json: {str(e)}")
    except IOError as e:
        raise RuntimeError(f"Error reading edgeEmbeddings.json: {str(e)}")


# -------------------------
# Core logic: compute edge times
# -------------------------
def get_edge_time(route, db_connection, embeddings_dict):
    logger.info(f"=== Starting get_edge_time ===")
    logger.info(f"Route received: {route}")
    logger.info(f"Route length: {len(route)}")
    logger.info(f"Embeddings dict has {len(embeddings_dict)} entries")
    
    times = []
    bucketsAvailable = set()

    cursor = db_connection.cursor()
    logger.debug("Database cursor created")
    
    try:
        placeholders = ",".join("?" * len(route))
        logger.debug(f"Querying database for buckets with placeholders: {placeholders}")
        cursor.execute(f"""
            SELECT DISTINCT traversal_id 
            FROM traversals 
            WHERE node_id IN ({placeholders})
        """, route)
        for row in cursor.fetchall():
            bucketsAvailable.add(int(row[0]))

        logger.info(f"Available buckets found: {sorted(bucketsAvailable)}")
        
        if not bucketsAvailable:
            logger.warning("No buckets available for any edge in route - returning empty list")
            return []

        chosen_bucket = random.choice(sorted(bucketsAvailable))
        logger.info(f"Chosen bucket for this route: {chosen_bucket}")

        for edgeId in route:
            edge = str(edgeId)
            logger.info(f"\n--- Processing edge: {edgeId} (type: {type(edgeId)}) ---")
            
            cursor.execute("""
                SELECT traversal_id, time_s 
                FROM traversals 
                WHERE node_id = ?
            """, (edgeId,))
            rows = cursor.fetchall()
            logger.debug(f"Edge {edgeId}: Found {len(rows)} traversal rows in database")

            if not rows:
                logger.warning(f"Edge {edgeId}: No traversals found in database - using fallback")
                # Embedding fallback
                found_time = None
                try:
                    if edge in embeddings_dict:
                        logger.debug(f"Edge {edgeId}: Found in embeddings_dict as '{edge}'")
                        target_vec = np.array(embeddings_dict[edge], dtype=np.float32)
                        logger.debug(f"Edge {edgeId}: Target vector shape: {target_vec.shape}")
                        
                        other_keys, other_vecs = zip(*[
                            (k, v) for k, v in embeddings_dict.items() if k != edge
                        ]) if len(embeddings_dict) > 1 else ([], [])

                        logger.debug(f"Edge {edgeId}: Built {len(other_vecs) if other_vecs else 0} comparison vectors")
                        
                        if other_vecs:
                            vecs = np.asarray(other_vecs, dtype=np.float32)
                            diffs = vecs - target_vec
                            dists = np.linalg.norm(diffs, axis=1)
                            order = np.argsort(dists)
                            logger.debug(f"Edge {edgeId}: Computed distances, min={dists.min():.4f}, max={dists.max():.4f}")

                            top_k = min(20, len(order))
                            nearest_edge_ids = [int(other_keys[int(order[i])]) for i in range(top_k)]
                            logger.info(f"Edge {edgeId}: Top {top_k} nearest neighbors: {nearest_edge_ids[:5]}... (showing first 5)")

                            placeholders_neighbors = ",".join("?" * len(nearest_edge_ids))
                            cursor.execute(f"""
                                SELECT DISTINCT node_id
                                FROM traversals 
                                WHERE node_id IN ({placeholders_neighbors})
                            """, nearest_edge_ids)
                            available_neighbors = {row[0] for row in cursor.fetchall()}
                            logger.debug(f"Edge {edgeId}: {len(available_neighbors)} of {top_k} nearest neighbors have traversal data")

                            for i in range(top_k):
                                other_edge_id = nearest_edge_ids[i]
                                if other_edge_id not in available_neighbors:
                                    continue
                                
                                logger.debug(f"Edge {edgeId}: Checking neighbor {other_edge_id} (distance rank {i})")
                                
                                cursor.execute("""
                                    SELECT traversal_id, time_s 
                                    FROM traversals 
                                    WHERE node_id = ?
                                """, (other_edge_id,))
                                neighbor_rows = cursor.fetchall()
                                if neighbor_rows:
                                    bucket_map = {int(r[0]): float(r[1]) for r in neighbor_rows}
                                    bucket_keys = sorted(bucket_map.keys())
                                    closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                                    found_time = bucket_map[closest_bucket]
                                    dist_val = float(dists[order[i]])
                                    logger.info(f"Edge {edgeId}: Using data from neighbor {other_edge_id} (distance={dist_val:.4f}, bucket={closest_bucket}, time={found_time:.2f}s)")
                                    break
                except Exception as e:
                    logger.error(f"Edge {edgeId}: Embedding fallback error: {e}")
                    print(f"Embedding fallback error for edge {edge}: {e}", file=sys.stderr)

                final_time = found_time if found_time is not None else 5.0
                times.append(final_time)
                if found_time is not None:
                    logger.info(f"Edge {edgeId}: Successfully added fallback time {final_time:.2f}s")
                else:
                    logger.warning(f"Edge {edgeId}: Using final fallback (5.0s)")
                continue

            # Normal path
            bucket_map = {int(row[0]): float(row[1]) for row in rows}
            bucket_keys = sorted(bucket_map.keys())
            logger.debug(f"Edge {edgeId}: Available buckets: {bucket_keys}")
            closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
            edge_time = bucket_map[closest_bucket]
            times.append(edge_time)
            logger.info(f"Edge {edgeId}: Using bucket {closest_bucket} with time {edge_time:.2f}s")

    finally:
        cursor.close()
        logger.debug("Database cursor closed")

    logger.info(f"=== Completed get_edge_time ===")
    logger.info(f"Computed {len(times)} times: {times}")
    logger.info(f"Total route time: {sum(times):.2f}s\n")
    return times


# -------------------------
# Public API: calculate traversal times for a route
# -------------------------
def EdgeTraversalTime(route, embedding_cache):
    logger.info(f"\n{'='*60}")
    logger.info(f"EdgeTraversalTime called")
    logger.info(f"Route: {route}")
    logger.info(f"Route length: {len(route) if route else 0}")
    logger.info(f"Embedding cache provided: {embedding_cache is not None}")
    logger.info(f"Embedding cache size: {len(embedding_cache) if embedding_cache else 0}")
    logger.info(f"{'='*60}\n")
    
    if not route:
        logger.error("Empty route provided")
        raise ValueError('No route data provided. (Empty Route)')
    if not embedding_cache:
        logger.error("Empty embedding cache provided")
        raise ValueError('Embedding cache is empty. (Missing Dataset)')

    try:
        logger.debug("Opening database connection...")
        with get_db_connection() as db_connection:
            logger.debug("Database connection established")
            result = get_edge_time(route, db_connection, embedding_cache)
            logger.info(f"EdgeTraversalTime returning {len(result)} times")
            return result
    except Exception as e:
        logger.error(f"Error in EdgeTraversalTime: {e}", exc_info=True)
        raise RuntimeError(f"Error calculating edge times: {e}")