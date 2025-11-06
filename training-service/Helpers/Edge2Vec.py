import json
import networkx as nx
from node2vec import Node2Vec
from gensim.models import KeyedVectors

DIMENSIONS = 64
WALK_LENGTH = 15
NUM_WALKS = 10
P = 1
Q = 1
WORKERS = 4
InputFile = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/RoadNetwork.json"
OutputFile = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/edgeEmbeddings.emb"

# Load vertex data
print("[INFO] Loading vertex graph...")
with open(InputFile, "r") as f:
    vertex_data = json.load(f)

# Build NEW graph using MultiDiGraph and edge IDs
G_vertex = nx.MultiDiGraph()
for src, info in vertex_data.items():
    src_id = int(src)
    edge_ids = info.get("outward_edges", [])
    targets = info.get("outward_vertices", [])

    if len(edge_ids) != len(targets):
        print(f"[WARN] Node {src_id} has mismatched lists: {len(edge_ids)} edges, {len(targets)} vertices")

    for edge_id, tgt in zip(edge_ids, targets):
        tgt_id = int(tgt)
        # Add each road as unique edge with edge_id as key
        G_vertex.add_edge(src_id, tgt_id, key=edge_id, edge_id=edge_id)

print(f"[INFO] New graph: {len(G_vertex.nodes())} nodes, {G_vertex.number_of_edges()} edges")

# Collect all unique edge IDs from dataset
dataset_edge_ids = {d['edge_id'] for u, v, k, d in G_vertex.edges(keys=True, data=True)}

# === Step 3: Convert to line graph (edges become nodes) ===
print("[INFO] Converting to line graph (edges become nodes)...")
G_edge = nx.line_graph(G_vertex)
print(f"[INFO] Line graph has {len(G_edge.nodes())} edge-nodes.")

# === Step 3a: Relabel line graph nodes to use original edge_ids ===
edge_to_id = {}
for u, v, k in G_edge.nodes():
    # Each node in line graph = an edge in original graph (u,v,k)
    edge_data = G_vertex.get_edge_data(u, v, k)
    edge_to_id[(u,v,k)] = str(edge_data['edge_id'])

G_edge = nx.relabel_nodes(G_edge, edge_to_id)

# === Step 4: Train Node2Vec on the line graph ===
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

# Save embeddings
model.wv.save_word2vec_format(OutputFile, write_header=False)
emb = KeyedVectors.load_word2vec_format(OutputFile, binary=False, no_header=True)

missing_edges = []
for edge_id in dataset_edge_ids:
    if str(edge_id) not in emb:
        missing_edges.append(edge_id)

OutputFileSorted = OutputFile

with open(OutputFile, "r") as f:
    lines = f.readlines()

lines_sorted = sorted(lines, key=lambda x: int(x.split()[0]))

with open(OutputFileSorted, "w") as f:
    f.writelines(lines_sorted)

print(f"[INFO] Sorted embeddings saved to {OutputFile}")