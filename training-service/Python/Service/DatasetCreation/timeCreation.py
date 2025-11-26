import random
from pathlib import Path
import numpy as np
import sqlite3
import json
import sys

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
    times = []
    bucketsAvailable = set()

    cursor = db_connection.cursor()
    try:
        placeholders = ",".join("?" * len(route))
        cursor.execute(f"""
            SELECT DISTINCT traversal_id 
            FROM traversals 
            WHERE node_id IN ({placeholders})
        """, route)
        for row in cursor.fetchall():
            bucketsAvailable.add(int(row[0]))

        if not bucketsAvailable:
            return []

        chosen_bucket = random.choice(sorted(bucketsAvailable))

        for edgeId in route:
            edge = str(edgeId)
            cursor.execute("""
                SELECT traversal_id, time_s 
                FROM traversals 
                WHERE node_id = ?
            """, (edgeId,))
            rows = cursor.fetchall()

            if not rows:
                # Embedding fallback
                found_time = None
                try:
                    if edge in embeddings_dict:
                        target_vec = np.array(embeddings_dict[edge], dtype=np.float32)
                        other_keys, other_vecs = zip(*[
                            (k, v) for k, v in embeddings_dict.items() if k != edge
                        ]) if len(embeddings_dict) > 1 else ([], [])

                        if other_vecs:
                            vecs = np.asarray(other_vecs, dtype=np.float32)
                            diffs = vecs - target_vec
                            dists = np.linalg.norm(diffs, axis=1)
                            order = np.argsort(dists)

                            top_k = min(20, len(order))
                            nearest_edge_ids = [int(other_keys[int(order[i])]) for i in range(top_k)]

                            placeholders_neighbors = ",".join("?" * len(nearest_edge_ids))
                            cursor.execute(f"""
                                SELECT DISTINCT node_id
                                FROM traversals 
                                WHERE node_id IN ({placeholders_neighbors})
                            """, nearest_edge_ids)
                            available_neighbors = {row[0] for row in cursor.fetchall()}

                            for i in range(top_k):
                                other_edge_id = nearest_edge_ids[i]
                                if other_edge_id not in available_neighbors:
                                    continue
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
                                    break
                except Exception as e:
                    print(f"Embedding fallback error for edge {edge}: {e}", file=sys.stderr)

                times.append(found_time if found_time is not None else 5.0)
                continue

            # Normal path
            bucket_map = {int(row[0]): float(row[1]) for row in rows}
            bucket_keys = sorted(bucket_map.keys())
            closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
            times.append(bucket_map[closest_bucket])

    finally:
        cursor.close()

    return times


# -------------------------
# Public API: calculate traversal times for a route
# -------------------------
def EdgeTraversalTime(route, embedding_cache):
    if not route:
        raise ValueError('No route data provided. (Empty Route)')
    if not embedding_cache:
        raise ValueError('Embedding cache is empty. (Missing Dataset)')

    try:
        with get_db_connection() as db_connection:
            return get_edge_time(route, db_connection, embedding_cache)
    except Exception as e:
        raise RuntimeError(f"Error calculating edge times: {e}")