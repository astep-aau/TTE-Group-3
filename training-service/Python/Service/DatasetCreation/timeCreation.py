import json
import random
from pathlib import Path
import numpy as np
import sqlite3
import sys
import logging

# Configure logging
logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)


def get_db_connection():
    """Open read-only connection to data.db"""
    db_path = Path(__file__).parent.parent / "Data" / "data.db"
    if not db_path.is_file():
        raise FileNotFoundError('"data.db" does not exist. (Missing Dataset)')

    try:
        conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, check_same_thread=False)

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


def get_all_embeddings(cursor):
    """Fetch all embeddings from database (for fallback neighbor search)"""
    cursor.execute("SELECT edge_id, vector FROM embeddings")
    rows = cursor.fetchall()
    return {row[0]: json.loads(row[1]) for row in rows}


def get_edge_time(route, db_connection):
    logger.info(f"=== Starting get_edge_time ===")
    logger.info(f"Route received: {route}")
    logger.info(f"Route length: {len(route)}")

    times = []
    bucketsAvailable = set()

    cursor = db_connection.cursor()
    logger.debug("Database cursor created")

    try:
        placeholders = ",".join("?" * len(route))
        logger.debug(f"Querying database for buckets")
        cursor.execute(f"""
            SELECT DISTINCT traversal_id
            FROM traversals
            WHERE edge_id IN ({placeholders})
        """, route)
        for row in cursor.fetchall():
            bucketsAvailable.add(int(row[0]))

        logger.info(f"Available buckets found: {sorted(bucketsAvailable)}")

        if not bucketsAvailable:
            logger.warning("No buckets available - returning empty list")
            return []

        chosen_bucket = random.choice(sorted(bucketsAvailable))
        logger.info(f"Chosen bucket: {chosen_bucket}")

        for edgeId in route:
            edge = str(edgeId)
            logger.info(f"\n--- Processing edge: {edgeId} ---")

            cursor.execute("""
                SELECT traversal_id, time_s
                FROM traversals
                WHERE edge_id = ?
            """, (edgeId,))
            rows = cursor.fetchall()
            logger.debug(f"Edge {edgeId}: Found {len(rows)} traversal rows")

            if not rows:
                logger.warning(f"Edge {edgeId}: No traversals - using fallback")

                found_time = None
                try:
                    # Get target vector from embeddings table
                    cursor.execute("SELECT vector FROM embeddings WHERE edge_id = ?", (edge,))
                    target_row = cursor.fetchone()

                    if target_row:
                        logger.debug(f"Edge {edgeId}: Found in embeddings table")
                        target_vec = np.array(json.loads(target_row[0]), dtype=np.float32)
                        logger.debug(f"Edge {edgeId}: Target vector shape: {target_vec.shape}")

                        # Load all other embeddings for comparison
                        all_embeddings = get_all_embeddings(cursor)
                        other_items = [(k, v) for k, v in all_embeddings.items() if k != edge]

                        logger.debug(f"Edge {edgeId}: Built {len(other_items)} comparison vectors")

                        if other_items:
                            other_keys, other_vecs = zip(*other_items)
                            vecs = np.asarray(other_vecs, dtype=np.float32)
                            diffs = vecs - target_vec
                            dists = np.linalg.norm(diffs, axis=1)
                            order = np.argsort(dists)
                            logger.debug(f"Edge {edgeId}: Distances min={dists.min():.4f}, max={dists.max():.4f}")

                            top_k = min(20, len(order))
                            nearest_edge_ids = [int(other_keys[int(order[i])]) for i in range(top_k)]
                            logger.info(f"Edge {edgeId}: Top {top_k} neighbors: {nearest_edge_ids[:5]}...")

                            placeholders_neighbors = ",".join("?" * len(nearest_edge_ids))
                            cursor.execute(f"""
                                SELECT DISTINCT edge_id
                                FROM traversals
                                WHERE edge_id IN ({placeholders_neighbors})
                            """, nearest_edge_ids)
                            available_neighbors = {row[0] for row in cursor.fetchall()}
                            logger.debug(f"Edge {edgeId}: {len(available_neighbors)}/{top_k} neighbors have data")

                            for i in range(top_k):
                                other_edge_id = nearest_edge_ids[i]
                                if other_edge_id not in available_neighbors:
                                    continue

                                logger.debug(f"Edge {edgeId}: Checking neighbor {other_edge_id}")

                                cursor.execute("""
                                    SELECT traversal_id, time_s
                                    FROM traversals
                                    WHERE edge_id = ?
                                """, (other_edge_id,))
                                neighbor_rows = cursor.fetchall()
                                if neighbor_rows:
                                    bucket_map = {int(r[0]): float(r[1]) for r in neighbor_rows}
                                    bucket_keys = sorted(bucket_map.keys())
                                    closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                                    found_time = bucket_map[closest_bucket]
                                    dist_val = float(dists[order[i]])
                                    logger.info(f"Edge {edgeId}: Using neighbor {other_edge_id} (dist={dist_val:.4f}, bucket={closest_bucket}, time={found_time:.2f}s)")
                                    break
                except (sqlite3.Error, json.JSONDecodeError, np.linalg.LinAlgError) as e:
                    logger.exception(f"Edge {edgeId}: Embedding fallback error")

                final_time = found_time if found_time is not None else 5.0
                times.append(final_time)
                if found_time is not None:
                    logger.info(f"Edge {edgeId}: Added fallback time {final_time:.2f}s")
                else:
                    logger.warning(f"Edge {edgeId}: Using final fallback (5.0s)")
                continue

            # Normal path with traversal data
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


def EdgeTraversalTime(route):
    """Calculate traversal times for a route using database connection"""
    logger.info(f"\n{'='*60}")
    logger.info(f"EdgeTraversalTime called")
    logger.info(f"Route: {route}")
    logger.info(f"Route length: {len(route) if route else 0}")
    logger.info(f"{'='*60}\n")

    if not route:
        logger.error("Empty route provided")
        raise ValueError('No route data provided. (Empty Route)')

    try:
        logger.debug("Opening database connection...")
        with get_db_connection() as db_conn:
            logger.debug("Database connection established")
            result = get_edge_time(route, db_conn)
            logger.info(f"EdgeTraversalTime returning {len(result)} times")
            return result
    except Exception as e:
        logger.error(f"Error in EdgeTraversalTime: {e}", exc_info=True)
        raise RuntimeError(f"Error calculating edge times: {e}")