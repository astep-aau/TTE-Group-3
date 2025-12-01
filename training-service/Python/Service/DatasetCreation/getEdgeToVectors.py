import json
import sqlite3
import math
import sys
from pathlib import Path

def get_db_connection():
    """Open a read-only connection to data.db"""
    db_path = Path(__file__).parent.parent / "Data" / "data.db"
    if not db_path.is_file():
        raise FileNotFoundError('"data.db" does not exist. (Missing Dataset)')
    
    try:
        conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, check_same_thread=False)
        
        cursor = conn.cursor()
        cursor.execute("PRAGMA integrity_check;")
        result = cursor.fetchone()
        cursor.close()
        if result[0] != "ok":
            conn.close()
            raise RuntimeError("Database is corrupted!")
        
        return conn
    except sqlite3.Error as e:
        raise RuntimeError(f"Database error: {e!r}") from e


def get_vector_from_db(edge_id: str, cursor):
    """Fetch a single vector from the database by edge_id"""
    cursor.execute("SELECT vector FROM embeddings WHERE edge_id = ?", (edge_id,))
    row = cursor.fetchone()
    
    if row is None:
        raise ValueError(f'No vector for edge {edge_id}. (Missing values)')
    
    return json.loads(row[0])


def apply_time_encoding(vector, timeBucket):
    """Appends sinusoidal time encoding to the vector if timeBucket is provided."""
    if timeBucket is None:
        return vector

    # Normalize time bucket (0-287) to 0-2pi
    time_angle = 2 * math.pi * timeBucket / 288.0
    sin_time = math.sin(time_angle)
    cos_time = math.cos(time_angle)
    
    # Append to vector
    return vector + [sin_time, cos_time]


def GetEdgeToVectors(edges, timeBucket=None):
    """
    Convert edge IDs to their vector embeddings from the database,
    optionally appending time encoding.
    """
    # Validate timeBucket
    if timeBucket is not None and (timeBucket < 0 or timeBucket > 287):
        raise ValueError(f"timeBucket must be between 0 and 287, got {timeBucket}")
    
    if not edges:
        raise ValueError('No route data provided. (Empty Route)')

    try:
        vectors = []
        
        with get_db_connection() as conn:
            cursor = conn.cursor()
            
            for edge in edges:
                # 1. Fetch raw vector from DB
                edge_str = str(edge)
                raw_vector = get_vector_from_db(edge_str, cursor)
                
                # 2. Apply time encoding
                final_vector = apply_time_encoding(raw_vector, timeBucket)
                
                vectors.append(final_vector)
            
            cursor.close()

        if not vectors:
            raise ValueError('No Vectors Converted. (Error in Conversion)')

        return vectors
        
    except Exception as e:
        # Preserve specific ValueErrors, wrap others
        if isinstance(e, ValueError):
            raise e
        raise RuntimeError(str(e))