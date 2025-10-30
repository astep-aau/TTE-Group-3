import sys
import json

# === CONFIGURATION ===
EMBEDDING_FILE = r"/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/edge_embeddings.emb"

def load_embeddings(filepath):
    embeddings = {}
    with open(filepath, "r") as f:
        for line in f:
            parts = line.strip().split()
            if len(parts) < 3:
                continue
            try:
                edge_id = int(parts[0])
                vector = [float(x) for x in parts[1:]]
                embeddings[edge_id] = vector
            except ValueError:
                continue
    return embeddings

def main():
    if len(sys.argv) < 2:
        print(json.dumps([]))
        return

    try:
        edge_ids = json.loads(sys.argv[1])
    except json.JSONDecodeError:
        print(json.dumps([]))
        return

    embeddings = load_embeddings(EMBEDDING_FILE)

    vectors = []
    for edge_id in edge_ids:
        vector = embeddings.get(edge_id, [0.0] * 128)
        vectors.append(vector)
        # If you need debug, send to stderr:
        # print(f"# Edge {edge_id} vector: {vector}", file=sys.stderr)

    # ONLY print JSON to stdout
    print(json.dumps(vectors))

if __name__ == "__main__":
    main()