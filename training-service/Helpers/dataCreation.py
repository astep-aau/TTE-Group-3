import json
import random

# -----------------------------
# Configurable parameters
# -----------------------------
# Default number of sequences to generate
NUM_SEQUENCES = 1000
SEQUENCE_LENGTH = random.randint(5, 25)

# -----------------------------
# Load the road network
# -----------------------------
data_path = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/RoadNetwork.json"  # Change path if needed

with open(data_path, "r") as f:
    graph_data = json.load(f)

# -----------------------------
# Function to generate a random sequence
# -----------------------------
def random_sequence(graph, length=5):
    if not graph:
        return []

    current_node = random.choice(list(graph.keys()))
    sequence = []
    visited_edges = set()  # Track edges already in sequence

    for _ in range(length):
        node_data = graph.get(current_node)
        if not node_data:
            break

        edges = node_data.get("outward_edges", [])
        vertices = node_data.get("outward_vertices", [])

        # Filter out edges we've already visited
        available = [(e, v) for e, v in zip(edges, vertices) if e not in visited_edges]
        if not available:
            break  # no new edges to visit

        chosen_edge, next_node = random.choice(available)
        sequence.append(chosen_edge)
        visited_edges.add(chosen_edge)
        current_node = str(next_node)

        if current_node not in graph:
            break

    return sequence

# -----------------------------
# Generate multiple sequences
# -----------------------------
all_sequences = [random_sequence(graph_data, SEQUENCE_LENGTH) for _ in range(NUM_SEQUENCES)]

# -----------------------------
# Output as JSON
# -----------------------------
print(json.dumps(all_sequences))