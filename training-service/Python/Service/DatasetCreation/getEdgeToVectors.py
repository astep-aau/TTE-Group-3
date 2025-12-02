import sys
from pathlib import Path

sys.path.append(str(Path(__file__).parent.parent / "Data"))
from LookupTableData.lookupManager import get_lookup_manager

def GetEdgeToVectors(edges, time_bucket=0):
    """Convert edge IDs to vectors using memory-mapped lookup tables"""
    try:
        if not edges:
            raise ValueError('No route data provided. (Empty Route)')

        manager = get_lookup_manager()
        vectors = []

        for edge in edges:
            edge_id = int(edge)  # Convert to int (sequential IDs)
            vector = manager.get_vector(edge_id)

            if vector is None:
                raise ValueError(f'No vector for edge {edge}. (Missing values)')

            # Convert numpy array to list and append time_bucket
            vector_list = vector.tolist()
            vector_list.append(float(time_bucket))
            vectors.append(vector_list)

        if not vectors:
            raise ValueError('No Vectors Converted. (Error in Conversion)')

        return vectors

    except Exception as e:
        raise RuntimeError(str(e))