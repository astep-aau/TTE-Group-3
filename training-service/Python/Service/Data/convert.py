import json
import sqlite3
from tqdm import tqdm

print("Starting conversion process...")

# --- Load JSON ---
print("Loading RoadTraversal.json...")
with open("RoadTraversal.json", "r") as f:
    data = json.load(f)
print(f"Loaded {len(data)} edges from JSON")

# --- Init SQLite ---
print("Connecting to data.db...")
conn = sqlite3.connect("data.db")
cur = conn.cursor()

# Create traversals table with edge_id
print("Creating/verifying traversals table...")
cur.execute("""
CREATE TABLE IF NOT EXISTS traversals (
    edge_id INTEGER,
    traversal_id INTEGER,
    time_s REAL,
    PRIMARY KEY (edge_id, traversal_id)
);
""")

# Check if length_cm exists in embeddings table
print("Checking embeddings table schema...")
cur.execute("PRAGMA table_info(embeddings)")
embeddings_columns = [column[1] for column in cur.fetchall()]

if "length_cm" not in embeddings_columns:
    print("Adding length_cm column to embeddings table...")
    cur.execute("ALTER TABLE embeddings ADD COLUMN length_cm INTEGER")
    conn.commit()
else:
    print("length_cm column already exists in embeddings table")

# Insert traversal data and update embeddings with length
print("Processing edges and traversals...")
traversals_inserted = 0
lengths_updated = 0

for edge_id, edge in tqdm(data.items(), desc="Processing edges", unit="edge"):
    eid = int(edge_id)
    length = edge.get("length (cm)")

    # Update embeddings table with length (replace existing data)
    cur.execute("""
        UPDATE embeddings SET length_cm = ? WHERE edge_id = ?
    """, (length, eid))
    
    if cur.rowcount > 0:
        lengths_updated += 1

    # Insert traversals
    if "traversals" in edge:
        for trav_id, trav in edge["traversals"].items():
            cur.execute("""
                INSERT OR REPLACE INTO traversals (edge_id, traversal_id, time_s)
                VALUES (?, ?, ?)
            """, (
                eid,
                int(trav_id),
                trav.get("time to traverse (s)")
            ))
            traversals_inserted += 1

conn.commit()
print(f"Committed {traversals_inserted} traversals and {lengths_updated} length updates")

# Create indexes for speed
print("Creating indexes...")
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_edge ON traversals(edge_id)")
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_pair ON traversals(edge_id, traversal_id)")

conn.commit()
conn.close()

print("=" * 50)
print("Conversion complete → data.db")
print(f"  Edges processed: {len(data)}")
print(f"  Traversals inserted: {traversals_inserted}")
print(f"  Lengths updated: {lengths_updated}")
print("=" * 50)