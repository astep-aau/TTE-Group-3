import json
import sqlite3

# --- Load JSON ---
with open("RoadTraversal.json", "r") as f:
    data = json.load(f)

# --- Init SQLite ---
conn = sqlite3.connect("traversals.db")
cur = conn.cursor()

# Create table
cur.execute("""
CREATE TABLE IF NOT EXISTS traversals (
    node_id INTEGER,
    traversal_id INTEGER,
    time_s REAL,
    PRIMARY KEY (node_id, traversal_id)
);
""")

# Insert data
for edge_id, edge in data.items():
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

conn.commit()

# Create indexes for speed
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_node ON traversals(node_id)")
cur.execute("CREATE INDEX IF NOT EXISTS idx_trav_pair ON traversals(node_id, traversal_id)")

conn.commit()
conn.close()

print("Conversion complete → traversals.db")