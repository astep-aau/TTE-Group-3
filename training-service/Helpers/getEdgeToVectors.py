import sys
import json

InputFile = r"/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/edgeEmbeddings.emb"

def load_embeddings(filepath):
    embeddings = {}
    with open(filepath, "r") as f:
        for line in f:
            parts = line.strip().split()
            if len(parts) < 2:
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
        # No input provided, return empty list
        print(json.dumps([]))
        return

    try:
        edge_ids = json.loads(sys.argv[1])
        if not isinstance(edge_ids, list):
            raise ValueError
    except (json.JSONDecodeError, ValueError):
        print(json.dumps([]))
        return

    # Load embeddings
    embeddings = load_embeddings(InputFile)

    # Lookup vectors
    vectors = []
    for edge_id in edge_ids:
        vector = embeddings.get(edge_id)  # edge_id must be int
        vectors.append(vector)
        if vector is None:
            print(f"[WARN] Missing embedding for edge {edge_id}", file=sys.stderr)

    # Output as JSON
    print(json.dumps(vectors))

if __name__ == "__main__":
    main()