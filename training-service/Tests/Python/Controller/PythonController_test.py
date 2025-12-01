import sys
from pathlib import Path
import json
import pytest

# ----------------------------
# Add the training-service folder to sys.path
# ----------------------------
sys.path.append(str(Path(__file__).resolve().parents[3]))

from Python.Controller.PythonController import _initialize_resources

# ----------------------------
# Test valid loading
# ----------------------------
def test_initialize_resources_valid(tmp_path):
    data = {"edge1": [0.1, 0.2], "edge2": [0.3, 0.4]}
    file_path = tmp_path / "LookupTableData" / "edgeEmbeddings.json"
    file_path.write_text(json.dumps(data))

    result = _initialize_resources(file_path)
    assert result == data

# ----------------------------
# Test missing file
# ----------------------------
def test_initialize_resources_missing(tmp_path):
    missing_file = tmp_path / "LookupTableData" / "missing.json"  # does not exist
    with pytest.raises(FileNotFoundError):
        _initialize_resources(missing_file)

# ----------------------------
# Test invalid JSON
# ----------------------------
def test_initialize_resources_invalid_json(tmp_path):
    invalid_file = tmp_path / "LookupTableData" / "edgeEmbeddings.json"
    invalid_file.write_text("{invalid_json: true}")  # malformed

    with pytest.raises(ValueError):
        _initialize_resources(invalid_file)

# ----------------------------
# Test empty JSON
# ----------------------------
def test_initialize_resources_empty_json(tmp_path):
    empty_file = tmp_path / "edgeEmbeddings.json"
    empty_file.write_text("")  # empty

    with pytest.raises(ValueError):
        _initialize_resources(empty_file)