import random
import numpy as np
import logging
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).parent.parent / "Data"))
from LookupTableData.lookupManager import get_lookup_manager

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)


def get_edge_time(route, manager, time_bucket):
    logger.info(f"=== Starting get_edge_time ===")
    logger.info(f"Route: {route}, Length: {len(route)}, TimeBucket: {time_bucket}")

    times = []
    bucketsAvailable = manager.get_available_buckets(route)

    logger.info(f"Available buckets: {sorted(bucketsAvailable)}")

    if not bucketsAvailable:
        logger.warning("No buckets available")
        return []

    # Use the provided time_bucket
    chosen_bucket = time_bucket
    logger.info(f"Chosen bucket: {chosen_bucket}")

    for edgeId in route:
        logger.info(f"\n--- Processing edge: {edgeId} ---")

        traversals = manager.get_traversals(edgeId)

        if not traversals:
            logger.warning(f"Edge {edgeId}: No traversals - using fallback")

            found_time = None
            try:
                target_vec = manager.get_vector(edgeId)

                if target_vec is not None:
                    all_vecs = manager.get_all_vectors()
                    
                    # Compute distances (vectorized)
                    diffs = all_vecs - target_vec
                    dists = np.linalg.norm(diffs, axis=1)
                    order = np.argsort(dists)

                    # Try top 20 neighbors (skip self at index 0)
                    for i in range(1, min(21, len(order))):
                        neighbor_edge_id = int(order[i])  # Index IS edge_id

                        neighbor_traversals = manager.get_traversals(neighbor_edge_id)
                        if neighbor_traversals:
                            bucket_map = {t[0]: t[1] for t in neighbor_traversals}
                            closest_bucket = min(bucket_map.keys(), key=lambda k: abs(k - chosen_bucket))
                            found_time = bucket_map[closest_bucket]
                            logger.info(f"Edge {edgeId}: Using neighbor {neighbor_edge_id} (dist={dists[neighbor_edge_id]:.4f}, time={found_time:.2f}s)")
                            break

            except Exception as e:
                logger.exception(f"Edge {edgeId}: Fallback error")

            final_time = found_time if found_time is not None else 5.0
            times.append(final_time)
            continue

        # Normal path
        bucket_map = {t[0]: t[1] for t in traversals}
        closest_bucket = min(bucket_map.keys(), key=lambda k: abs(k - chosen_bucket))
        edge_time = bucket_map[closest_bucket]
        times.append(edge_time)
        logger.info(f"Edge {edgeId}: Using bucket {closest_bucket}, time={edge_time:.2f}s")

    return times


def EdgeTraversalTime(route, time_bucket=0):
    if not route:
        raise ValueError('No route data provided. (Empty Route)')

    try:
        manager = get_lookup_manager()
        return get_edge_time(route, manager, time_bucket)
    except Exception as e:
        logger.error(f"Error: {e}", exc_info=True)
        raise RuntimeError(f"Error calculating edge times: {e}") from e