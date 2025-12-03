import sqlite3
import numpy as np
import json
import csv
from pathlib import Path

def convert_db_to_lookup_tables():
    """Convert SQLite database to memory-mapped NumPy arrays and CSV"""
    db_path = Path(__file__).parent / "data.db"
    output_dir = Path(__file__).parent / "LookupTableData"
    output_dir.mkdir(exist_ok=True)

    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()
    
    print("Converting embeddings...")
    # Get all embeddings ordered by edge_id
    cursor.execute("SELECT edge_id, vector FROM embeddings ORDER BY CAST(edge_id AS INTEGER)")
    rows = cursor.fetchall()

    # Verify edge IDs are sequential starting from 0
    expected_ids = list(range(len(rows)))
    actual_ids = [int(row[0]) for row in rows]
    
    if actual_ids != expected_ids:
        print("⚠️  Warning: Edge IDs are not sequential from 0!")
        print(f"   Expected: {expected_ids[:10]}...")
        print(f"   Actual:   {actual_ids[:10]}...")
        # Fall back to mapping file
        edge_to_idx = {str(edge_id): idx for idx, edge_id in enumerate(actual_ids)}
        with open(output_dir / "edge_mapping.json", 'w') as f:
            json.dump(edge_to_idx, f, separators=(',', ':'))
        print("   Created edge_mapping.json as fallback")
    else:
        print("✓ Edge IDs are sequential from 0 - no mapping file needed")

    # Save vectors as memory-mapped numpy array
    vectors = [json.loads(row[1]) for row in rows]
    vectors_array = np.array(vectors, dtype=np.float32)
    np.save(output_dir / "embeddings.npy", vectors_array)

    print(f"✓ Embeddings: {len(vectors)} vectors × {len(vectors[0])} dims = {vectors_array.nbytes / 1024:.1f}KB")

    print("\nConverting traversals...")
    cursor.execute("""
        SELECT edge_id, traversal_id, time_s
        FROM traversals
        ORDER BY edge_id, traversal_id
    """)

    with open(output_dir / "traversals.csv", 'w', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['edge_id', 'traversal_id', 'time_s'])

        count = 0
        for row in cursor:
            writer.writerow(row)
            count += 1
            if count % 500000 == 0:
                print(f"  Written {count:,} rows...")

    print(f"✓ Traversals: {count:,} rows")

    conn.close()
    print(f"\nFiles created in {output_dir}/")
    print(f"  - embeddings.npy (memory-mapped)")
    print(f"  - traversals.csv")

if __name__ == "__main__":
    convert_db_to_lookup_tables()