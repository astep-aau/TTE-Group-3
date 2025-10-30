import json
import sys
import random

# -----------------------------
# Load the traversal dataset
# -----------------------------
data_path = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/RoadTraversal.json"

with open(data_path, "r") as f:
    traversal_data = json.load(f)

edge_sequence = json.loads(sys.argv[1])

# -----------------------------
# Function to get time for a single edge given a chosen bucket
# -----------------------------
def get_edge_time(edge_id, chosen_bucket):
    edge_str = str(edge_id)
    if edge_str not in traversal_data:
        return 0.0

    traversals = traversal_data[edge_str].get("traversals", {})
    if not traversals:
        return 0.0

    bucket_keys = sorted(int(k) for k in traversals.keys())

    # Pick the closest available bucket
    closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
    key_to_use = str(closest_bucket)

    # Debug info
    #print(f"# Edge {edge_id}, chosen {chosen_bucket}, using bucket {key_to_use}", file=sys.stderr)

    return traversals[key_to_use]["time to traverse (s)"]


# -----------------------------
# Pick a single random bucket for the whole sequence
# -----------------------------
all_buckets = set()
for edge_id in edge_sequence:
    edge_str = str(edge_id)
    if edge_str in traversal_data:
        all_buckets.update(int(k) for k in traversal_data[edge_str].get("traversals", {}).keys())

if not all_buckets:
    print(json.dumps([0.0] * len(edge_sequence)))
    sys.exit(0)

chosen_bucket = random.choice(sorted(all_buckets))

# -----------------------------
# Compute traversal times for all edges
# -----------------------------
times = [get_edge_time(e, chosen_bucket) for e in edge_sequence]
#print(f"{times}", file=sys.stderr)

# -----------------------------
# Output the list of times
# -----------------------------
print(json.dumps(times))
