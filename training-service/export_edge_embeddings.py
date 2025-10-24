"""
export_edge_embeddings.py
-------------------------
Converts a trained Edge2Vec model + mapping file into a NumPy array
for direct use in machine learning models (e.g., LSTMs).

Outputs:
- edge_embeddings.npy : dense embedding matrix (shape: [num_edges, embedding_dim])
"""

import json
import numpy as np
from gensim.models import Word2Vec

# === Configuration ===
MODEL_PATH = "C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge2vec_model.model"
MAPPING_PATH = "C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge_id_mapping.json"
OUTPUT_PATH = "C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge_embeddings.npy"

print("[INFO] Loading model and mapping...")
model = Word2Vec.load(MODEL_PATH)

with open(MAPPING_PATH, "r") as f:
    mapping = json.load(f)

embedding_dim = model.vector_size
num_edges = len(mapping)

print(f"[INFO] Building embedding matrix: {num_edges} edges × {embedding_dim} dimensions")

# Create embedding matrix
emb_matrix = np.zeros((num_edges, embedding_dim), dtype=np.float32)

for edge_str, idx in mapping.items():
    if edge_str in model.wv:
        emb_matrix[idx] = model.wv[edge_str]
    else:
        # This should almost never happen unless mappings are mismatched
        print(f"[WARN] Missing embedding for edge {edge_str}, filling with zeros.")

# Save matrix
np.save(OUTPUT_PATH, emb_matrix)
print(f"[INFO] Saved embedding matrix to {OUTPUT_PATH}")
print("[DONE] Export complete.")
