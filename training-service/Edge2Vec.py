"""
edge2vec_train.py
-----------------
Train Edge2Vec embeddings from a vertex-based graph (like a road network).

Each road segment (edge) becomes a "node" in the line graph, so Node2Vec learns
embeddings that represent the relationships between connected roads.

Outputs:
- edge2vec_model.model   (full gensim Word2Vec model)
- edge_embeddings.emb    (embeddings in text format)
- edge_id_mapping.json   (optional: mapping edge_id → index)
"""

import json
import networkx as nx
from node2vec import Node2Vec

# === Configuration ===
VERTEX_DATA_PATH = "training-service\\vertex_data.json"    # Input file
DIMENSIONS = 128                         # Size of embedding vector
WALK_LENGTH = 80                         # Steps per random walk
NUM_WALKS = 10                           # Walks per edge-node
P = 1                                    # Return parameter
Q = 1                                    # In-out parameter
WORKERS = 4                              # Parallel threads
OUTPUT_MODEL = "edge2vec_model.model"
OUTPUT_EMBEDDINGS = "edge_embeddings.emb"
OUTPUT_MAPPING = "edge_id_mapping.json"

# === Step 1: Load vertex graph ===
print("[INFO] Loading vertex graph...")
with open(VERTEX_DATA_PATH, "r") as f:
    vertex_data = json.load(f)

# Build original directed graph (nodes = intersections, edges = roads)
G_vertex = nx.DiGraph()
for src, info in vertex_data.items():
    src_id = int(src)
    for tgt in info.get("outward_vertices", []):
        G_vertex.add_edge(src_id, int(tgt))

print(f"[INFO] Loaded vertex graph with {len(G_vertex.nodes())} nodes and {len(G_vertex.edges())} edges.")

# === Step 2: Convert to line graph (edges become nodes) ===
# In a line graph: each edge (u→v) in G becomes a node in G_line
# Two "edge-nodes" in G_line are connected if they share a common vertex in G
print("[INFO] Converting to line graph (edges become nodes)...")
G_edge = nx.line_graph(G_vertex)
print(f"[INFO] Line graph has {len(G_edge.nodes())} edge-nodes.")

# === Step 3: Train Node2Vec on the line graph ===
print("[INFO] Training Edge2Vec model...")
node2vec = Node2Vec(
    G_edge,
    dimensions=DIMENSIONS,
    walk_length=WALK_LENGTH,
    num_walks=NUM_WALKS,
    p=P,
    q=Q,
    workers=WORKERS
)

model = node2vec.fit(window=10, min_count=1, batch_words=4)
print("[INFO] Training complete.")

# === Step 4: Save results ===
print(f"[INFO] Saving model to {OUTPUT_MODEL}...")
model.save(OUTPUT_MODEL)
model.wv.save_word2vec_format(OUTPUT_EMBEDDINGS)

# Create an edge-id mapping for later lookup
edge_to_index = {str(edge): i for i, edge in enumerate(G_edge.nodes())}
with open(OUTPUT_MAPPING, "w") as f:
    json.dump(edge_to_index, f, indent=2)

print(f"[INFO] Saved embeddings and mapping successfully.")
print("[DONE] Edge2Vec training complete.")
