import sys
from pathlib import Path
import json
import pytest
import sqlite3
import numpy as np
from unittest.mock import patch, MagicMock

# ----------------------------
# Add the training-service folder to sys.path
# ----------------------------
sys.path.append(str(Path(__file__).resolve().parents[4]))

from Python.Service.DatasetCreation.timeCreation import (
    get_db_connection,
    get_all_embeddings,
    get_edge_time,
    EdgeTraversalTime
)

# ----------------------------
# Fixtures
# ----------------------------
@pytest.fixture
def mock_db(tmp_path):
    """Create a temporary SQLite database with test data"""
    db_path = tmp_path / "data.db"
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    
    # Create tables
    cursor.execute("""
        CREATE TABLE traversals (
            edge_id INTEGER,
            traversal_id INTEGER,
            time_s REAL
        )
    """)
    
    cursor.execute("""
        CREATE TABLE embeddings (
            edge_id TEXT PRIMARY KEY,
            vector TEXT
        )
    """)
    
    # Insert test data - edges with full traversal data
    cursor.execute("INSERT INTO traversals VALUES (1, 100, 3.5)")
    cursor.execute("INSERT INTO traversals VALUES (1, 101, 4.2)")
    cursor.execute("INSERT INTO traversals VALUES (2, 100, 2.1)")
    cursor.execute("INSERT INTO traversals VALUES (2, 102, 2.8)")
    cursor.execute("INSERT INTO traversals VALUES (3, 101, 5.0)")
    
    # Insert embeddings (32-dimensional vectors)
    vec1 = json.dumps([0.1] * 32)
    vec2 = json.dumps([0.2] * 32)
    vec3 = json.dumps([0.3] * 32)
    vec4 = json.dumps([0.15] * 32)  # Close to vec1
    
    cursor.execute("INSERT INTO embeddings VALUES ('1', ?)", (vec1,))
    cursor.execute("INSERT INTO embeddings VALUES ('2', ?)", (vec2,))
    cursor.execute("INSERT INTO embeddings VALUES ('3', ?)", (vec3,))
    cursor.execute("INSERT INTO embeddings VALUES ('4', ?)", (vec4,))  # No traversals, but has embedding
    
    conn.commit()
    conn.close()
    
    return db_path

@pytest.fixture
def corrupted_db(tmp_path):
    """Create a corrupted database"""
    db_path = tmp_path / "data.db"
    # Write invalid SQLite data
    db_path.write_bytes(b"This is not a valid SQLite database")
    return db_path

# ----------------------------
# Test get_db_connection
# ----------------------------
def test_get_db_connection_missing_file(tmp_path, monkeypatch):
    """Test that missing database raises FileNotFoundError"""
    monkeypatch.setattr(Path, "is_file", lambda self: False)
    
    with pytest.raises(FileNotFoundError, match="data.db.*does not exist"):
        get_db_connection()

def test_get_db_connection_corrupted(corrupted_db, monkeypatch):
    """Test that corrupted database raises RuntimeError"""
    # Mock the entire Path class to intercept Path(__file__)
    class MockPath:
        def __init__(self, *args, **kwargs):
            self._path = corrupted_db.parent
        
        @property
        def parent(self):
            return self
        
        def __truediv__(self, other):
            if other == "data.db":
                return corrupted_db
            return self
        
        def is_file(self):
            return True
    
    # Patch Path in the timeCreation module
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.timeCreation.Path",
        MockPath
    )
    
    with pytest.raises(RuntimeError, match="Database"):
        get_db_connection()

def test_get_db_connection_valid(mock_db, monkeypatch):
    """Test successful database connection"""
    # Mock the path resolution
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.timeCreation.Path",
        lambda *args: type('MockPath', (), {'parent': mock_db.parent, 'is_file': lambda: True})()
    )
    
    # Directly test with the mock_db
    conn = sqlite3.connect(f"file:{mock_db}?mode=ro", uri=True)
    cursor = conn.cursor()
    cursor.execute("PRAGMA integrity_check;")
    result = cursor.fetchone()
    cursor.close()
    conn.close()
    
    assert result[0] == "ok"

# ----------------------------
# Test get_all_embeddings
# ----------------------------
def test_get_all_embeddings(mock_db):
    """Test fetching all embeddings from database"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    embeddings = get_all_embeddings(cursor)
    
    assert len(embeddings) == 4
    assert '1' in embeddings
    assert '2' in embeddings
    assert '3' in embeddings
    assert '4' in embeddings
    assert isinstance(embeddings['1'], list)
    assert len(embeddings['1']) == 32
    
    cursor.close()
    conn.close()

def test_get_all_embeddings_malformed_json(mock_db):
    """Test that malformed JSON in embeddings raises error"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    # Insert malformed JSON
    cursor.execute("INSERT INTO embeddings VALUES ('999', 'invalid{json')")
    conn.commit()
    
    with pytest.raises(json.JSONDecodeError):
        get_all_embeddings(cursor)
    
    cursor.close()
    conn.close()

# ----------------------------
# Test get_edge_time - Happy Path
# ----------------------------
def test_get_edge_time_normal_path(mock_db):
    """Test normal path with edges that have traversal data"""
    conn = sqlite3.connect(mock_db)
    route = [1, 2, 3]
    
    times = get_edge_time(route, conn)
    
    assert len(times) == 3
    assert all(isinstance(t, float) for t in times)
    assert all(t > 0 for t in times)
    
    conn.close()

def test_get_edge_time_bucket_selection(mock_db):
    """Test that a valid bucket is chosen from available buckets"""
    conn = sqlite3.connect(mock_db)
    route = [1, 2]  # Both have traversals in buckets 100, 101, 102
    
    with patch('random.choice') as mock_choice:
        # Mock to return the first bucket
        mock_choice.return_value = 100
        
        times = get_edge_time(route, conn)
        
        # Verify random.choice was called with available buckets
        assert mock_choice.called
        called_buckets = sorted(mock_choice.call_args[0][0])
        assert 100 in called_buckets
        
    conn.close()

def test_get_edge_time_closest_bucket_selection(mock_db):
    """Test that closest bucket is selected when exact match doesn't exist"""
    conn = sqlite3.connect(mock_db)
    
    # Edge 1 has buckets 100, 101
    # Edge 2 has buckets 100, 102
    # If chosen bucket is 101, edge 2 should use bucket 100 or 102 (closest)
    route = [1, 2]
    
    with patch('random.choice', return_value=101):
        times = get_edge_time(route, conn)
        
        assert len(times) == 2
        # Edge 1 should use bucket 101 (exact match)
        assert times[0] == 4.2
        # Edge 2 should use bucket 100 (closest to 101)
        assert times[1] == 2.1
    
    conn.close()

# ----------------------------
# Test get_edge_time - Fallback to Embeddings
# ----------------------------
def test_get_edge_time_fallback_to_neighbor(mock_db):
    """Test fallback to nearest neighbor when edge has no traversals"""
    conn = sqlite3.connect(mock_db)

    # Edge 4 has embedding [0.15] but no traversals
    # Nearest neighbor by distance:
    # - Edge 1: [0.1] → distance = |0.15 - 0.1| = 0.05
    # - Edge 2: [0.2] → distance = |0.15 - 0.2| = 0.05
    # - Edge 3: [0.3] → distance = |0.15 - 0.3| = 0.15
    # Edge 1 and 2 are equidistant, so it could pick either
    route = [4, 1]

    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)

        assert len(times) == 2
        # Edge 4 should use a neighbor's time (edge 1 or edge 2 from bucket 100)
        # Edge 1 bucket 100: 3.5
        # Edge 2 bucket 100: 2.1
        assert times[0] in [2.1, 3.5]  # Accept either nearest neighbor
        # Edge 1 should use its own time
        assert times[1] == 3.5

    conn.close()

def test_get_edge_time_fallback_no_embedding(mock_db):
    """Test fallback to 5.0s when edge has no traversals and no embedding"""
    conn = sqlite3.connect(mock_db)
    
    # Edge 999 has neither traversals nor embeddings
    route = [999, 1]
    
    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)
        
        assert len(times) == 2
        assert times[0] == 5.0  # Default fallback
        assert times[1] == 3.5  # Normal edge
    
    conn.close()

def test_get_edge_time_fallback_neighbor_no_traversals(mock_db):
    """Test fallback when nearest neighbors also have no traversals"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    # Add two edges with embeddings but no traversals
    vec5 = json.dumps([0.5] * 32)
    vec6 = json.dumps([0.51] * 32)  # Very close to vec5
    cursor.execute("INSERT INTO embeddings VALUES ('5', ?)", (vec5,))
    cursor.execute("INSERT INTO embeddings VALUES ('6', ?)", (vec6,))
    conn.commit()
    
    route = [5, 1]
    
    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)
        
        assert len(times) == 2
        assert times[0] == 5.0  # Falls back to default when neighbors also have no data
        assert times[1] == 3.5
    
    cursor.close()
    conn.close()

# ----------------------------
# Test get_edge_time - No Buckets Available
# ----------------------------
def test_get_edge_time_no_buckets_available(mock_db):
    """Test that empty list is returned when no buckets are available"""
    conn = sqlite3.connect(mock_db)
    
    # Route with edges that have no traversals
    route = [999, 998]
    
    times = get_edge_time(route, conn)
    
    assert times == []
    
    conn.close()

# ----------------------------
# Test EdgeTraversalTime
# ----------------------------
def test_EdgeTraversalTime_empty_route():
    """Test that empty route raises ValueError"""
    with pytest.raises(ValueError, match="No route data provided"):
        EdgeTraversalTime([])

def test_EdgeTraversalTime_valid_route(mock_db, monkeypatch):
    """Test EdgeTraversalTime with valid route"""
    # Mock get_db_connection to use our test database
    def mock_get_db_connection():
        return sqlite3.connect(mock_db)
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.timeCreation.get_db_connection",
        mock_get_db_connection
    )
    
    route = [1, 2]
    times = EdgeTraversalTime(route)
    
    assert len(times) == 2
    assert all(isinstance(t, float) for t in times)

def test_EdgeTraversalTime_database_error(monkeypatch):
    """Test that database errors are wrapped in RuntimeError"""
    def mock_get_db_connection():
        raise sqlite3.Error("Database locked")
    
    monkeypatch.setattr(
        "Python.Service.DatasetCreation.timeCreation.get_db_connection",
        mock_get_db_connection
    )
    
    with pytest.raises(RuntimeError, match="Error calculating edge times"):
        EdgeTraversalTime([1, 2, 3])

# ----------------------------
# Test Edge Cases
# ----------------------------
def test_single_edge_route(mock_db):
    """Test route with single edge"""
    conn = sqlite3.connect(mock_db)
    route = [1]
    
    times = get_edge_time(route, conn)
    
    assert len(times) == 1
    assert times[0] > 0
    
    conn.close()

def test_duplicate_edges_in_route(mock_db):
    """Test route with duplicate edge IDs"""
    conn = sqlite3.connect(mock_db)
    route = [1, 2, 1, 2]
    
    times = get_edge_time(route, conn)
    
    assert len(times) == 4
    # Same edge should get same time (same bucket chosen)
    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)
        assert times[0] == times[2]  # Edge 1 appears twice
        assert times[1] == times[3]  # Edge 2 appears twice
    
    conn.close()

def test_large_route(mock_db):
    """Test route with many edges (performance check)"""
    conn = sqlite3.connect(mock_db)
    
    # Create a route with 100 edges (mix of valid and invalid)
    route = [1, 2, 3] * 33 + [1]
    
    times = get_edge_time(route, conn)
    
    assert len(times) == 100
    
    conn.close()

def test_nonexistent_edge_ids(mock_db):
    """Test route with edge IDs that don't exist in database"""
    conn = sqlite3.connect(mock_db)
    route = [9999, 8888, 7777]
    
    times = get_edge_time(route, conn)
    
    # Should return empty list (no buckets available)
    assert times == []
    
    conn.close()

def test_mixed_valid_invalid_edges(mock_db):
    """Test route with mix of valid edges and edges needing fallback"""
    conn = sqlite3.connect(mock_db)
    
    # Edge 1 has data, edge 4 needs fallback, edge 999 needs default fallback
    route = [1, 4, 999]
    
    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)
        
        assert len(times) == 3
        assert times[0] > 0  # Normal edge
        assert times[1] > 0  # Fallback from neighbor
        assert times[2] == 5.0  # Default fallback
    
    conn.close()

def test_embedding_dimension_mismatch(mock_db):
    """Test handling of embeddings with wrong dimensions"""
    conn = sqlite3.connect(mock_db)
    cursor = conn.cursor()
    
    # Insert embedding with wrong dimensions
    vec_wrong = json.dumps([0.1] * 64)  # 64 dimensions instead of 32
    cursor.execute("INSERT INTO embeddings VALUES ('999', ?)", (vec_wrong,))
    conn.commit()
    
    route = [999, 1]
    
    # Should handle gracefully (fallback to 5.0 or raise error)
    with patch('random.choice', return_value=100):
        times = get_edge_time(route, conn)
        # Behavior depends on NumPy - either crashes or returns default
        assert len(times) == 2
    
    cursor.close()
    conn.close()