import sys
import json
from pathlib import Path

def convertEdgeToVector(embeddings, edges):
    vectors = []
    for edge in edges:
        vector = embeddings.get(str(edge))
        if vector is None:
            raise ValueError('No vector for that Edge. (Missing values)')
        vectors.append(vector)
    return vectors

def GetEdgeToVectors(edges, embedding_cache=None):
    try:
        if not edges:
            raise ValueError('No route data provided. (Empty Route)')

        # Prefer controller-provided cache
        if embedding_cache is None:
            InputFile = Path(__file__).parent.parent / "Data" / "edgeEmbeddings.json"
            if not InputFile.is_file():
                raise FileNotFoundError(f'"edgeEmbeddings.json" does not exist. (Missing dataset)')
            with open(InputFile, "r") as f:
                Embeddings = json.load(f)
        else:
            Embeddings = embedding_cache

        vectors = convertEdgeToVector(Embeddings, edges)

        if not vectors:
            raise ValueError('No Vectors Converted. (Error in Conversion)')

        return vectors
    except Exception as e:
        raise RuntimeError(str(e))