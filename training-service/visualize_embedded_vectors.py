"""
visualize_embedded_vectors_interactive.py
----------------------------------------
Visualize Edge2Vec embeddings in 2D (PCA) and inspect edge IDs interactively.
Hover over any dot to see the (u,v) edge it represents.
"""

import matplotlib.pyplot as plt
import numpy as np
from sklearn.decomposition import PCA
from gensim.models import Word2Vec
import mplcursors  # <-- this adds interactive hover labels

# === Load model ===
MODEL_PATH = "C:\\Users\\mathi\\Documents\\GitHub\\TTE-Group-3\\edge2vec_model.model"
TARGET_EDGE = "(290573873, 290576137)"  # example edge

print("[INFO] Loading model...")
model = Word2Vec.load(MODEL_PATH)
edges = list(model.wv.key_to_index.keys())  # include all edges

print(f"[INFO] Loaded {len(edges)} edges with dimension {model.vector_size}")

# --- Collect vectors ---
vectors = np.array([model.wv[e] for e in edges])

# --- Reduce to 2D with PCA ---
print("[INFO] Reducing dimensions with PCA...")
pca = PCA(n_components=2)
proj = pca.fit_transform(vectors)

# --- Plot setup ---
plt.figure(figsize=(9, 7))
sc = plt.scatter(proj[:, 0], proj[:, 1], s=8, alpha=0.4, label="Edges")

# Highlight target + neighbors
if TARGET_EDGE in model.wv:
    neighbors = [sim for sim, _ in model.wv.most_similar(TARGET_EDGE, topn=5)]
    def plot_point(edge, color, label):
        idx = edges.index(edge)
        plt.scatter(proj[idx, 0], proj[idx, 1], s=120, color=color, label=label,
                    edgecolors='black', linewidths=1)

    plot_point(TARGET_EDGE, "red", "Target edge")
    for nb in neighbors:
        plot_point(nb, "blue", "Similar edge")

plt.title("Edge2Vec Embeddings (2D PCA Projection)")
plt.xlabel("PCA 1")
plt.ylabel("PCA 2")
plt.legend()

# === Enable interactive tooltips ===
print("[INFO] Enabling hover tooltips...")
cursor = mplcursors.cursor(hover=True)
@cursor.connect("add")
def on_add(sel):
    i = sel.index
    sel.annotation.set(text=edges[i], fontsize=8, backgroundcolor="white")

plt.show()
