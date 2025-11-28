import json
import sqlite3

# --- Load JSON ---
with open("RoadTraversal.json", "r") as f:
    data = json.load(f)

# --- Init SQLite ---
conn = sqlite3.connect("data.db")
cur = conn.cursor()

# Create traversals table with edge_id
cur.execute("""
CREATE TABLE IF NOT EXISTS traversals (
    edge_id INTEGER,
    traversal_id INTEGER,
    time_s REAL,
    PRIMARY KEY (edge_id, traversal_id)
);
""")

# Check if length_cm exists in embeddings table
cur.execute("PRAGMA table_info(embeddings)")
embeddings_columns = [column[1] for column in cur.fetchall()]

if "length_cm" not in embeddings_columns:
    print("Adding length_cm column to embeddings table...")
    cur.execute("ALTER TABLE embeddings ADD COLUMN length_cm INTEGER")
    conn.commit()

# Insert traversal data and update embeddings with length
for edge_id, edge in data.items():
    eid = int(edge_id)
    length = edge.get("length (cm)")

    # Update embeddings table with length (replace existing data)
    cur.execute("""
        UPDATE embeddings SET length_cm = ? WHERE edge_id = ?
    """, (length, eid))

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

conn.commit()

# Create indexes for speed
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_edge ON traversals(edge_id)")
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_pair ON traversals(edge_id, traversal_id)")

conn.commit()
conn.close()

print("Conversion complete → data.db")