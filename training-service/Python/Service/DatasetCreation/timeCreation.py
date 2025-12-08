import numpy as np
import logging
import sys
import csv
from pathlib import Path

sys.path.append(str(Path(__file__).parent.parent / "Data"))
from LookupTableData.lookupManager import get_lookup_manager

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO)

SPEED_SMOOTHING_ENABLED = True
SPEED_PERCENTILE_CLIP = (5, 90) 

# Cache for edge lengths (edge_id -> length_cm)
_edge_lengths_cache = None

def _load_edge_lengths():
    """Load edge lengths from CSV file (cached)."""
    global _edge_lengths_cache
    if _edge_lengths_cache is not None:
        return _edge_lengths_cache
    
    # Try multiple possible locations for the edge lengths file
    possible_paths = [
        Path(__file__).parent.parent / "Data" / "LookupTableData" / "edge_lengths.csv",
        Path(__file__).parent.parent.parent.parent.parent / "routeestimation-service" / "Datasets" / "edge_traversals_lengths.csv",
    ]
    
    csv_path = None
    for path in possible_paths:
        if path.exists():
            csv_path = path
            break
    
    if csv_path is None:
        logger.warning("Edge lengths file not found - speed smoothing disabled")
        _edge_lengths_cache = {}
        return _edge_lengths_cache
    
    _edge_lengths_cache = {}
    with open(csv_path, 'r') as f:
        reader = csv.DictReader(f)
        for row in reader:
            edge_id = int(row['edge_id'])
            length_cm = float(row['length_cm'])
            _edge_lengths_cache[edge_id] = length_cm
    
    logger.info(f"Loaded {len(_edge_lengths_cache)} edge lengths from {csv_path}")
    return _edge_lengths_cache

# Cache for speed bounds (computed from all traversal data)
_speed_bounds_cache = None

def _compute_speed_bounds(manager, edge_lengths):
    """
    Compute speed percentile bounds from all available traversal data.
    Speed = length_cm / time_s (cm/s)
    """
    global _speed_bounds_cache
    if _speed_bounds_cache is not None:
        return _speed_bounds_cache
    
    if not edge_lengths:
        _speed_bounds_cache = (None, None)
        return _speed_bounds_cache
    
    all_speeds = []
    for edge_id in edge_lengths.keys():
        traversals = manager.get_traversals(edge_id)
        if traversals and edge_id in edge_lengths:
            length_cm = edge_lengths[edge_id]
            for _, time_s in traversals:
                if time_s > 0:
                    speed = length_cm / time_s  # cm/s
                    all_speeds.append(speed)
    
    if not all_speeds:
        logger.warning("No traversal data for speed bounds computation")
        _speed_bounds_cache = (None, None)
        return _speed_bounds_cache
    
    all_speeds = np.array(all_speeds)
    lower_bound = np.percentile(all_speeds, SPEED_PERCENTILE_CLIP[0])
    upper_bound = np.percentile(all_speeds, SPEED_PERCENTILE_CLIP[1])
    
    _speed_bounds_cache = (lower_bound, upper_bound)
    logger.info(f"Speed bounds (cm/s): [{lower_bound:.2f}, {upper_bound:.2f}] "
                f"({lower_bound * 0.036:.1f} - {upper_bound * 0.036:.1f} km/h)")
    return _speed_bounds_cache

def _smooth_edge_time(edge_id, raw_time, edge_lengths, speed_bounds):
    """
    Smooth a single edge traversal time based on speed bounds.
    
    If the implied speed is outside the bounds, clip it and recalculate time.
    """
    if not SPEED_SMOOTHING_ENABLED:
        return raw_time
    
    if edge_id not in edge_lengths:
        return raw_time  # No length data, can't smooth
    
    lower_speed, upper_speed = speed_bounds
    if lower_speed is None or upper_speed is None:
        return raw_time
    
    length_cm = edge_lengths[edge_id]
    if raw_time <= 0:
        return raw_time
    
    raw_speed = length_cm / raw_time  # cm/s
    
    # Clip speed to bounds
    clipped_speed = np.clip(raw_speed, lower_speed, upper_speed)
    
    # Recalculate time from clipped speed
    smoothed_time = length_cm / clipped_speed
    
    if abs(smoothed_time - raw_time) > 0.1:
        logger.debug(f"Edge {edge_id}: smoothed {raw_time:.2f}s -> {smoothed_time:.2f}s "
                    f"(speed {raw_speed:.1f} -> {clipped_speed:.1f} cm/s)")
    
    return smoothed_time


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

    # Load edge lengths and compute speed bounds for smoothing
    edge_lengths = _load_edge_lengths()
    speed_bounds = _compute_speed_bounds(manager, edge_lengths)

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

            raw_time = found_time if found_time is not None else 5.0
            # Apply speed-based smoothing
            smoothed_time = _smooth_edge_time(edgeId, raw_time, edge_lengths, speed_bounds)
            times.append(smoothed_time)
            continue

        # Normal path
        bucket_map = {t[0]: t[1] for t in traversals}
        closest_bucket = min(bucket_map.keys(), key=lambda k: abs(k - chosen_bucket))
        raw_edge_time = bucket_map[closest_bucket]
        
        # Apply speed-based smoothing
        smoothed_time = _smooth_edge_time(edgeId, raw_edge_time, edge_lengths, speed_bounds)
        times.append(smoothed_time)
        logger.info(f"Edge {edgeId}: Using bucket {closest_bucket}, raw={raw_edge_time:.2f}s, smoothed={smoothed_time:.2f}s")

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
