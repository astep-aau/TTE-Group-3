import sys
from pathlib import Path
import json
import pytest
import sqlite3
from unittest.mock import patch

# ----------------------------
# Add the training-service folder to sys.path
# ----------------------------
sys.path.append(str(Path(__file__).resolve().parents[4]))

from Python.Service.DatasetCreation.getEdgeToVectors import (
    get_db_connection,
    get_vector_from_db,
    GetEdgeToVectors
)

# ----------------------------
# Fixtures
# ----------------------------
@pytest.fixture
def mock_db(tmp_path):
    """Create a temporary SQLite database with test embedding data"""
    db_path = tmp_path / "data.db"
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    
    # Create embeddings table
    cursor.execute("""
        CREATE TABLE embeddings (
            edge_id TEXT PRIMARY KEY,
            vector TEXT
        )
    """)
    
    # Insert test embeddings (32-dimensional vectors with distinct values)
    vec1 = json.dumps([0.1 * i for i in range(32)])  # [0.0, 0.1, 0.2, ...]
    vec2 = json.dumps([0.2 * i for i in range(32)])  # [0.0, 0.2, 0.4, ...]
    vec3 = json.dumps([0.3 * i for i in range(32)])  # [0.0, 0.3, 0.6, ...]
    vec_single = json.dumps([1.5] * 32)  # Single value repeated
    
    cursor.execute("INSERT INTO embeddings VALUES ('1', ?)", (vec1,))
    cursor.execute("INSERT INTO embeddings VALUES ('2', ?)", (vec2,))
    cursor.execute("INSERT INTO embeddings VALUES ('3', ?)", (vec3,))
    cursor.execute("INSERT INTO embeddings VALUES ('100', ?)", (vec_single,))
    
    # Edge with string ID (not just numeric)
    vec_str = json.dumps([0.5] * 32)
    cursor.execute("INSERT INTO embeddings VALUES ('edge_abc', ?)", (vec_str,))
    
    conn.commit()
    conn.close()
    
    return db_path

@pytest.fixture
def malformed_db(tmp_path):
    """Create a database with malformed data"""
    db_path = tmp_path / "data.db"
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    
    cursor.execute("""
        CREATE TABLE embeddings (
            edge_id TEXT PRIMARY KEY,
            vector TEXT
        )
    """)
    
    # Insert various malformed data
    cursor.execute("INSERT INTO embeddings VALUES ('bad_json', 'not{valid]json')")
    cursor.execute("INSERT INTO embeddings VALUES ('wrong_dim', ?)", 
                   (json.dumps([0.1] * 64),))  # Wrong dimension
    cursor.execute("INSERT INTO embeddings VALUES ('empty_vec', '[]')")
    cursor.execute("INSERT INTO embeddings VALUES ('non_numeric', ?)", 
                   (json.dumps(['a', 'b', 'c']),))
    
    conn.commit()
    conn.close()
    
    return db_path

@pytest.fixture
def corrupted_db(tmp_path):
    """Create a corrupted database file"""
    db_path = tmp_path / "data.db"
    db_path.write_bytes(b"This is not a valid SQLite database")
    return db_path

# ----------------------------
# Test get_db_connection
# ----------------------------
def test_get_db_connection_missing_file(monkeypatch):
    """Test that missing database raises FileNotFoundError"""
    class MockPath:
        def __init__(self, *args, **kwargs):
            pass
        
        @property
        def parent(self):
            return self
        
        def __truediv__(self, other):
            return self
        
        def is_file(self):
            return False
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.Path",
        MockPath
    )
    
    with pytest.raises(FileNotFoundError, match="data.db.*does not exist"):
        get_db_connection()

def test_get_db_connection_corrupted(corrupted_db, monkeypatch):
    """Test that corrupted database raises RuntimeError"""
    # Patch sqlite3.connect to force an error with corrupted database
    original_connect = sqlite3.connect

    def mock_connect(db_path, **kwargs):
        conn = original_connect(str(corrupted_db), **kwargs)
        # Force SQLite to actually read the file by running integrity check
        # This will fail with corrupted database
        cursor = conn.cursor()
        cursor.execute("PRAGMA integrity_check;")
        cursor.close()
        return conn

    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.sqlite3.connect",
        mock_connect
    )

    # Also need to make is_file() return True
    class MockPath:
        def __init__(self, *args, **kwargs):
            pass

        @property
        def parent(self):
            return self

        def __truediv__(self, other):
            return self

        def is_file(self):
            return True

    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.Path",
        MockPath
    )

    with pytest.raises(RuntimeError, match="Database error"):
        get_db_connection()

def test_get_db_connection_valid(mock_db, monkeypatch):
    """Test successful database connection"""
    class MockPath:
        def __init__(self, *args, **kwargs):
            pass
        
        @property
        def parent(self):
            return self
        
        def __truediv__(self, other):
            if other == "data.db":
                return mock_db
            return self
        
        def is_file(self):
            return True
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.Path",
        MockPath
    )
    
    conn = get_db_connection()
    assert conn is not None
    
    # Verify it's read-only by trying to write
    cursor = conn.cursor()
    with pytest.raises(sqlite3.OperationalError):
        cursor.execute("INSERT INTO embeddings VALUES ('test', '[]')")
    
    cursor.close()
    conn.close()

# ----------------------------
# Test get_vector_from_db
# ----------------------------
def test_get_vector_from_db_valid(mock_db):
    """Test fetching valid vector from database"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    vector = get_vector_from_db('1', cursor)
    
    assert isinstance(vector, list)
    assert len(vector) == 32
    assert vector[0] == 0.0
    assert vector[1] == 0.1
    assert vector[31] == 3.1
    
    cursor.close()
    conn.close()

def test_get_vector_from_db_missing_edge(mock_db):
    """Test that missing edge raises ValueError"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    with pytest.raises(ValueError, match="No vector for edge 999"):
        get_vector_from_db('999', cursor)
    
    cursor.close()
    conn.close()

def test_get_vector_from_db_string_edge_id(mock_db):
    """Test fetching vector with string edge ID"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    vector = get_vector_from_db('edge_abc', cursor)
    
    assert isinstance(vector, list)
    assert len(vector) == 32
    assert all(v == 0.5 for v in vector)
    
    cursor.close()
    conn.close()

def test_get_vector_from_db_malformed_json(malformed_db):
    """Test that malformed JSON raises JSONDecodeError"""
    conn = sqlite3.connect(malformed_db)
    cursor = conn.cursor()
    
    with pytest.raises(json.JSONDecodeError):
        get_vector_from_db('bad_json', cursor)
    
    cursor.close()
    conn.close()

# ----------------------------
# Test GetEdgeToVectors - Happy Path
# ----------------------------
def test_GetEdgeToVectors_normal_path(mock_db, monkeypatch):
    """Test normal conversion of edges to vectors"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1, 2, 3]
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 3
    assert all(isinstance(v, list) for v in vectors)
    assert all(len(v) == 32 for v in vectors)
    
    # Verify order matches input
    assert vectors[0][1] == 0.1  # Edge 1
    assert vectors[1][1] == 0.2  # Edge 2
    assert vectors[2][1] == 0.3  # Edge 3

def test_GetEdgeToVectors_preserves_order(mock_db, monkeypatch):
    """Test that vector order matches edge order"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [3, 1, 2]  # Different order
    vectors = GetEdgeToVectors(edges)
    
    # Verify order
    assert vectors[0][1] == 0.3  # Edge 3
    assert vectors[1][1] == 0.1  # Edge 1
    assert vectors[2][1] == 0.2  # Edge 2

def test_GetEdgeToVectors_returns_list_of_floats(mock_db, monkeypatch):
    """Test that vectors contain float values"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1, 2]
    vectors = GetEdgeToVectors(edges)
    
    for vector in vectors:
        assert all(isinstance(v, (int, float)) for v in vector)

# ----------------------------
# Test GetEdgeToVectors - Edge ID Types
# ----------------------------
def test_GetEdgeToVectors_integer_edge_ids(mock_db, monkeypatch):
    """Test with integer edge IDs"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1, 2, 100]
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 3

def test_GetEdgeToVectors_string_edge_ids(mock_db, monkeypatch):
    """Test with string edge IDs"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = ['1', '2', 'edge_abc']
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 3

def test_GetEdgeToVectors_mixed_edge_types(mock_db, monkeypatch):
    """Test with mixed integer and string edge IDs"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1, '2', 'edge_abc', 100]
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 4

def test_GetEdgeToVectors_special_characters(mock_db, monkeypatch):
    """Test with special characters in edge IDs (should fail as not in DB)"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = ['edge-with-dashes', 'edge/with/slashes']
    
    with pytest.raises(RuntimeError, match="No vector for edge"):
        GetEdgeToVectors(edges)

# ----------------------------
# Test GetEdgeToVectors - Empty/Invalid Input
# ----------------------------
def test_GetEdgeToVectors_none_input():
    """Test that None input raises RuntimeError"""
    with pytest.raises(RuntimeError):
        GetEdgeToVectors(None)

def test_GetEdgeToVectors_empty_list():
    """Test that empty list raises ValueError"""
    with pytest.raises(RuntimeError, match="No route data provided"):
        GetEdgeToVectors([])

def test_GetEdgeToVectors_single_edge(mock_db, monkeypatch):
    """Test with single edge"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1]
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 1
    assert len(vectors[0]) == 32

def test_GetEdgeToVectors_large_route(mock_db, monkeypatch):
    """Test with large number of edges (1000+)"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    # Create 1000 edges by repeating existing ones
    edges = [1, 2, 3] * 334
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 1002

def test_GetEdgeToVectors_duplicate_edges(mock_db, monkeypatch):
    """Test with duplicate edge IDs"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    edges = [1, 2, 1, 2, 1]
    vectors = GetEdgeToVectors(edges)
    
    assert len(vectors) == 5
    # Verify same edge gives same vector
    assert vectors[0] == vectors[2] == vectors[4]  # All edge 1
    assert vectors[1] == vectors[3]  # Both edge 2

# ----------------------------
# Test GetEdgeToVectors - Database Issues
# ----------------------------
def test_GetEdgeToVectors_missing_database(monkeypatch):
    """Test with missing database file"""
    def mock_get_db_connection():
        raise FileNotFoundError('"data.db" does not exist. (Missing Dataset)')
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError, match="data.db.*does not exist"):
        GetEdgeToVectors([1, 2, 3])

def test_GetEdgeToVectors_corrupted_database(monkeypatch):
    """Test with corrupted database"""
    def mock_get_db_connection():
        raise RuntimeError("Database error: file is not a database")
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError, match="Database error"):
        GetEdgeToVectors([1, 2, 3])

def test_GetEdgeToVectors_database_locked(monkeypatch):
    """Test with database locked error"""
    def mock_get_db_connection():
        raise RuntimeError("Database error: database is locked")
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError, match="Database error"):
        GetEdgeToVectors([1, 2, 3])

def test_GetEdgeToVectors_connection_closes_properly(mock_db, monkeypatch):
    """Test that database connection is closed after use"""
    connection_closed = False

    class ConnectionWrapper:
        def __init__(self, conn):
            self._conn = conn
            self._closed = False

        def cursor(self):
            if self._closed:
                raise sqlite3.ProgrammingError("Cannot operate on a closed database")
            return self._conn.cursor()

        def close(self):
            self._closed = True
            self._conn.close()

        def __enter__(self):
            return self

        def __exit__(self, *args):
            nonlocal connection_closed
            connection_closed = True
            self.close()

    def mock_get_db_connection():
        conn = sqlite3.connect(mock_db)
        return ConnectionWrapper(conn)

    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )

    edges = [1, 2]
    GetEdgeToVectors(edges)

    # Verify the context manager exit was called (connection closed)
    assert connection_closed, "Connection was not closed properly"

# ----------------------------
# Test GetEdgeToVectors - Malformed Data
# ----------------------------
def test_GetEdgeToVectors_malformed_json(malformed_db, monkeypatch):
    """Test with malformed JSON in database"""
    def mock_get_db_connection():
        return sqlite3.connect(malformed_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError):
        GetEdgeToVectors(['bad_json'])

def test_GetEdgeToVectors_wrong_dimension(malformed_db, monkeypatch):
    """Test with vectors of wrong dimensions"""
    def mock_get_db_connection():
        return sqlite3.connect(malformed_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    # Should still succeed - dimension validation is not enforced
    vectors = GetEdgeToVectors(['wrong_dim'])
    assert len(vectors) == 1
    assert len(vectors[0]) == 64  # Wrong dimension, but still returned

def test_GetEdgeToVectors_empty_vector(malformed_db, monkeypatch):
    """Test with empty vector array"""
    def mock_get_db_connection():
        return sqlite3.connect(malformed_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    vectors = GetEdgeToVectors(['empty_vec'])
    assert len(vectors) == 1
    assert vectors[0] == []

def test_GetEdgeToVectors_non_numeric_values(malformed_db, monkeypatch):
    """Test with non-numeric values in vector"""
    def mock_get_db_connection():
        return sqlite3.connect(malformed_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    vectors = GetEdgeToVectors(['non_numeric'])
    assert len(vectors) == 1
    # JSON decodes successfully, but values are strings
    assert vectors[0] == ['a', 'b', 'c']

# ----------------------------
# Test GetEdgeToVectors - Missing Edges
# ----------------------------
def test_GetEdgeToVectors_missing_single_edge(mock_db, monkeypatch):
    """Test with single missing edge"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError, match="No vector for edge 999"):
        GetEdgeToVectors([999])

def test_GetEdgeToVectors_mixed_existing_missing(mock_db, monkeypatch):
    """Test with mix of existing and missing edges"""
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.getEdgeToVectors.get_db_connection",
        mock_get_db_connection
    )
    
    # Should fail on first missing edge
    with pytest.raises(RuntimeError, match="No vector for edge"):
        GetEdgeToVectors([1, 2, 999, 3])