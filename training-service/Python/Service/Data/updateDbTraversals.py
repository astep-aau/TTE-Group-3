import json
import sqlite3
from pathlib import Path

# --- Paths ---
data_dir = Path(__file__).parent
traversals_json = data_dir / "RoadTraversal.json"
output_db = data_dir / "data.db"

# --- Load JSON file ---
if not traversals_json.is_file():
    raise FileNotFoundError(f'"{traversals_json.name}" does not exist.')

with open(traversals_json, "r") as f:
    traversals_data = json.load(f)

# --- Init SQLite ---
conn = sqlite3.connect(output_db)
cur = conn.cursor()

try:
    # Create traversals table if it doesn't exist
    cur.execute("""
    CREATE TABLE IF NOT EXISTS traversals (
        node_id INTEGER,
        traversal_id INTEGER,
        time_s REAL,
        PRIMARY KEY (node_id, traversal_id)
    );
    """)
    
    print("Clearing existing traversals...")
    cur.execute("DELETE FROM traversals")
    
    # Insert traversals in a single transaction
    print("Inserting traversals...")
    insert_count = 0
    for edge_id, edge in traversals_data.items():
        eid = int(edge_id)
        if "traversals" in edge:
            for trav_id, trav in edge["traversals"].items():
                cur.execute("""
                    INSERT INTO traversals (node_id, traversal_id, time_s)
                    VALUES (?, ?, ?)
                """, (
                    eid,
                    int(trav_id),
                    trav.get("time to traverse (s)")
                ))
                insert_count += 1
    
    # Create indexes for performance
    print("Creating indexes...")
    cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_node ON traversals(node_id)")
    cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_pair ON traversals(node_id, traversal_id)")
    
    conn.commit()
    
    print(f"✅ Conversion complete → {output_db}")
    print(f"   - Traversals inserted: {insert_count} rows from {len(traversals_data)} edges")

except Exception as e:
    conn.rollback()
    print(f"❌ Error during conversion (rolled back): {e}")
    raise

finally:
    cur.close()
    conn.close()