import numpy as np
import csv
from pathlib import Path
from threading import Lock
from typing import List, Tuple, Optional, Dict

class LookupTableManager:
    """
    Thread-safe singleton for efficient database lookups.
    Optimized for sequential edge IDs (0, 1, 2, ...).
    """
    _instance = None
    _lock = Lock()

    def __new__(cls):
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = super().__new__(cls)
                    cls._instance._initialized = False
        return cls._instance

    def __init__(self):
        if self._initialized:
            return

        with self._lock:
            if self._initialized:
                return

            # Since lookupManager.py is in Data/LookupTableData/, 
            # just use the parent directory (LookupTableData/)
            data_dir = Path(__file__).parent

            # Memory-mapped embeddings (edge_id is direct index)
            embeddings_path = data_dir / "embeddings.npy"
            self._embeddings = np.load(embeddings_path, mmap_mode='r')
            self._max_edge_id = len(self._embeddings) - 1

            # Build traversals index
            self._traversals_index = self._build_traversals_index(data_dir / "traversals.csv")
            self._traversals_file = data_dir / "traversals.csv"

            self._initialized = True

    def _build_traversals_index(self, csv_path: Path) -> Dict[int, Tuple[int, int]]:
        """Build index of edge_id -> (start_byte, end_byte)"""
        index = {}
        with open(csv_path, 'r') as f:
            # Skip header and record its byte position
            header = f.readline()
            
            current_edge = None
            start_pos = f.tell()
            
            # Read line by line using readline() instead of iterator
            while True:
                line = f.readline()
                if not line:  # EOF
                    break
                    
                edge_id = int(line.split(',')[0])
                
                if edge_id != current_edge:
                    if current_edge is not None:
                        # End position is just before this line
                        index[current_edge] = (start_pos, f.tell() - len(line.encode('utf-8')))
                    current_edge = edge_id
                    start_pos = f.tell() - len(line.encode('utf-8'))
            
            # Handle last edge
            if current_edge is not None:
                index[current_edge] = (start_pos, f.tell())
        
        return index

    def get_vector(self, edge_id: int) -> Optional[np.ndarray]:
        """
        Get embedding vector for edge_id.
        Since edge IDs are sequential, edge_id IS the array index.
        """
        if edge_id < 0 or edge_id > self._max_edge_id:
            return None
        return self._embeddings[edge_id]

    def get_all_vectors(self) -> np.ndarray:
        """Get all vectors (for nearest neighbor fallback)"""
        return self._embeddings

    def get_edge_ids(self) -> range:
        """Get all edge IDs (just a range from 0 to max)"""
        return range(self._max_edge_id + 1)

    def get_traversals(self, edge_id: int) -> List[Tuple[int, float]]:
        """Get all (traversal_id, time_s) pairs for an edge"""
        if edge_id not in self._traversals_index:
            return []  # Normal - no data for this edge
    
        start, end = self._traversals_index[edge_id]
    
        with open(self._traversals_file, 'r') as f:
            f.seek(start)
            data = f.read(end - start)
    
        results = []
        for line in data.strip().split('\n'):
            if not line.strip():
                continue
    
            parts = line.split(',')
    
            # Skip malformed rows (empty fields or wrong column count)
            if len(parts) != 3 or not parts[1].strip() or not parts[2].strip():
                continue
    
            try:
                results.append((int(parts[1]), float(parts[2])))
            except (ValueError, IndexError):
                continue  # Skip invalid data
    
        return results

    def get_available_buckets(self, route: List[int]) -> set:
        """Get all traversal_ids that exist for ANY edge in the route"""
        buckets = set()
        for edge_id in route:
            traversals = self.get_traversals(edge_id)
            buckets.update(t[0] for t in traversals)
        return buckets


# Global singleton
_lookup_manager = None
_manager_lock = Lock()

def get_lookup_manager() -> LookupTableManager:
    """Thread-safe singleton getter"""
    global _lookup_manager
    if _lookup_manager is None:
        with _manager_lock:
            if _lookup_manager is None:
                _lookup_manager = LookupTableManager()
    return _lookup_manager