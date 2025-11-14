import json
import math
import pandas as pd


# ------------------------
#  Load CSVs
# ------------------------
vertex_df = pd.read_csv("vertex.csv")
edge_conn_df = pd.read_csv("edge_connections.csv")
edge_data_df = pd.read_csv("edge_data_averaged.csv")

# ------------------------
#  Helper: haversine distance (m)
# ------------------------
def haversine(lat1, lon1, lat2, lon2):
    """
    Great-circle distance between two (lat, lon) in meters.
    """
    R = 6371000.0  # Earth radius in meters
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    dphi = math.radians(lat2 - lat1)
    dlambda = math.radians(lon2 - lon1)

    a = (
            math.sin(dphi / 2) ** 2
            + math.cos(phi1) * math.cos(phi2) * math.sin(dlambda / 2) ** 2
    )
    c = 2 * math.asin(math.sqrt(a))
    return R * c


# ============================================================
# A) VERTEX-BASED GRAPH (like your "290573873": {...} example)
# ============================================================

# Map node_id -> (lat, lon)  (note: csv is longitude,latitude)
coords = {
    int(row.node_id): (row.latitude, row.longitude)
    for row in vertex_df.itertuples(index=False)
}

vertex_graph = {}

for row in edge_conn_df.itertuples(index=False):
    edge_id = int(row.edge_id)
    start = int(row.vertex_start_id)
    end = int(row.vertex_end_id)

    # Ensure both vertices exist in dict
    if str(start) not in vertex_graph:
        vertex_graph[str(start)] = {
            "outward_edges": [],
            "backward_edges": [],
            "outward_vertices": [],
            "backward_vertices": [],
        }
    if str(end) not in vertex_graph:
        vertex_graph[str(end)] = {
            "outward_edges": [],
            "backward_edges": [],
            "outward_vertices": [],
            "backward_vertices": [],
        }

    # Outward from start -> end
    vertex_graph[str(start)]["outward_edges"].append(edge_id)
    vertex_graph[str(start)]["outward_vertices"].append(end)

    # Backward to end <- start
    vertex_graph[str(end)]["backward_edges"].append(edge_id)
    vertex_graph[str(end)]["backward_vertices"].append(start)



with open("vertex_graph.json", "w") as f:
    json.dump(vertex_graph, f, indent=2)

print("Wrote vertex-based graph to vertex_graph.json")


# ============================================================
# B) EDGE-BASED TRAVERSALS (like your edge '2': {...} example)
# ============================================================

# Precompute approximate edge length (cm) from coordinates
edge_length_cm = {}
for row in edge_conn_df.itertuples(index=False):
    eid = int(row.edge_id)
    s = int(row.vertex_start_id)
    t = int(row.vertex_end_id)

    if s in coords and t in coords:
        lat1, lon1 = coords[s]
        lat2, lon2 = coords[t]
        dist_m = haversine(lat1, lon1, lat2, lon2)
        edge_length_cm[eid] = int(round(dist_m * 100))
    else:
        edge_length_cm[eid] = None  # missing coordinates

edge_traversals = {}

# We’ll use the row index as the "time bucket" key (0..287),
# like "0", "2", "3" in your example.
# Only keep entries where the traversal time is >= 0 (ignoring -1 = no data).
for col in edge_data_df.columns:
    if col == "time_slot":
        continue

    # column name is e.g. 'edge123_traversal_time_sec'
    # extract the numeric ID between 'edge' and '_traversal_time_sec'
    edge_id = int(col[4:-19])
    edge_key = str(edge_id)

    traversals_for_edge = {}

    series = edge_data_df[col]
    # iterate over buckets (5-minute intervals), index is 0..287
    for bucket_idx, val in series.items():
        if val is None or val < 0:
            continue  # -1.0 = no traversal for that bucket

        bucket_key = str(bucket_idx)  # "0", "1", ... up to "287"

        traversals_for_edge[bucket_key] = {
            "time to traverse (s)": round(float(val), 2),
        }

    # Fill the main structure for this edge
    edge_traversals[edge_key] = {
        # You don't have type/oneway in the files; keep placeholders or
        # change to what you know from elsewhere (e.g. from OSM).
        "type": None,          # e.g. "trunk", "residential", ...
        "oneway": True,        # or False, depending on your network
        "length (cm)": edge_length_cm.get(edge_id),
        "traversals": traversals_for_edge,
    }

with open("edge_traversals.json", "w") as f:
    json.dump(edge_traversals, f, indent=2)

print("Wrote edge-based traversals to edge_traversals.json")
