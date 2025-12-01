import json
from pathlib import Path
from typing import Optional, Dict

# Global cache for embeddings
_embeddings_cache: Optional[Dict] = None

def get_embeddings() -> Dict:
    """
    Get the edge embeddings dictionary, loading from file if not already cached.
    This ensures the file is only loaded once and reused for all requests.
    """
    global _embeddings_cache
    
    if _embeddings_cache is None:
        EmbeddingFile = Path(__file__).parent.parent / "Data" / "edgeEmbeddings.json"
        
        if not EmbeddingFile.is_file():
            raise FileNotFoundError(f'"edgeEmbeddings.json" does not exist. (Missing dataset)')
        
        with open(EmbeddingFile, "r") as f:
            _embeddings_cache = json.load(f)
        
        if not _embeddings_cache:
            raise ValueError('"edgeEmbeddings.json" is empty or not loaded. (Empty Dataset)')
    
    return _embeddings_cache

