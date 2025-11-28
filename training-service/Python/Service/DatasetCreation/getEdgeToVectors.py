import json
import sqlite3
from pathlib import Path


def get_db_connection():
    """Open a read-only connection to data.db"""
    db_path = Path(__file__).parent.parent / "Data" / "data.db"
    if not db_path.is_file():
        raise FileNotFoundError('"data.db" does not exist. (Missing Dataset)')
    
    try:
        conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, check_same_thread=False)
        return conn
    except sqlite3.Error as e:
        raise RuntimeError(f"Database error: {e!r}") from e


def get_vector_from_db(edge_id: str, cursor):
    """Fetch a single vector from the database by edge_id"""
    cursor.execute("SELECT vector FROM embeddings WHERE edge_id = ?", (edge_id,))
    row = cursor.fetchone()
    
    if row is None:
        # TODO: Consider returning None or a default vector instead of raising
        raise ValueError(f'No vector for edge {edge_id}. (Missing values)')
    
    return json.loads(row[0])


def GetEdgeToVectors(edges):
    """
    Convert edge IDs to their vector embeddings from the database.
    Uses individual queries to minimize memory usage.
    """
    try:
        if not edges: #Check if the data is empty, if it is raise an error.
            raise ValueError('No route data provided. (Empty Route)')

        vectors = []
        
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            for edge in edges:
                edge_str = str(edge)
                vector = get_vector_from_db(edge_str, cursor)
                vectors.append(vector)
            
            cursor.close()

        if not vectors:
            raise ValueError('No Vectors Converted. (Error in Conversion)')

        return vectors
        
    except Exception as e:
        raise RuntimeError(str(e))