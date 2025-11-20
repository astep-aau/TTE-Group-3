import os
import math
import json
import pandas as pd

# -------------------------------
# CONFIG
# -------------------------------

# Folder where all CSVs live (use script's directory)
DATA_DIR = os.path.dirname(os.path.abspath(__file__))

# Daily files you want to convert -> one JSON per file
DAY_FILES = [
    "edge_data_day3.csv",
    "edge_data_day4.csv",
    "edge_data_day5.csv",
    "edge_data_day6.csv",
    "edge_data_day7.csv",
]

# Filenames for shared data
VERTEX_FILE = "vertex.csv"
EDGE_CONNECTIONS_FILE = "edge_connections.csv"
AVERAGED_FILE = "edge_data_averaged.csv"

# Use averaged file to fill in missing values (marked as -1, etc.)
USE_AVERAGE_FALLBACK = True

# -------------------------------
# Helpers
# -------------------------------

def haversine_cm(lat1, lon1, lat2, lon2):
    """
    Compute distance between (lat1, lon1) and (lat2, lon2) in centimeters
    using the haversine formula.
    """
    R = 6371000.0  # Earth radius in meters
    # Convert deg -> rad
    lat1_rad = math.radians(lat1)
    lon1_rad = math.radians(lon1)
    lat2_rad = math.radians(lat2)
    lon2_rad = math.radians(lon2)

    dlat = lat2_rad - lat1_rad
    dlon = lon2_rad - lon1_rad

    a = math.sin(dlat / 2.0) ** 2 + math.cos(lat1_rad) * math.cos(lat2_rad) * math.sin(dlon / 2.0) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1.0 - a))

    distance_m = R * c
    distance_cm = distance_m * 100.0
    return distance_cm

def build_edge_metadata(vertex_path, edge_connections_path):
    """
    Read vertex + edge_connections and compute length (cm) for each edge.
    Returns: dict[edge_id] = {"type": None, "oneway": True, "length (cm)": float or None}
    """
    vertex_df = pd.read_csv(vertex_path)
    edge_df = pd.read_csv(edge_connections_path)

    # Index vertices by node_id for fast lookup
    vertex_df = vertex_df.set_index("node_id")

    edge_meta = {}

    for _, row in edge_df.iterrows():
        edge_id = int(row["edge_id"])
        start_id = row["vertex_start_id"]
        end_id = row["vertex_end_id"]

        length_cm = None
        try:
            start = vertex_df.loc[start_id]
            end = vertex_df.loc[end_id]
            length_cm = haversine_cm(
                lat1=start["latitude"],
                lon1=start["longitude"],
                lat2=end["latitude"],
                lon2=end["longitude"],
            )
        except KeyError:
            # If a vertex is missing, we just leave length as None
            length_cm = None

        edge_meta[edge_id] = {
            "type": None,       # you can change this if you have real types
            "oneway": True,     # adjust if you know which edges are bidirectional
            "length (cm)": length_cm,
        }

    return edge_meta

def parse_edge_id_from_col(col_name):
    """
    Parse edge id from a column name like 'edge123_traversal_time_sec'.
    """
    prefix = "edge"
    suffix = "_traversal_time_sec"
    if not (col_name.startswith(prefix) and col_name.endswith(suffix)):
        return None
    id_part = col_name[len(prefix):-len(suffix)]
    return int(id_part)

def build_json_for_day(day_df, avg_df, edge_meta, use_average_fallback=True):
    """
    Convert a day's DataFrame to the desired JSON-able dict structure.
    """
    result = {}

    # Loop over all edge columns (skip 'time_slot')
    for col in day_df.columns:
        if col == "time_slot":
            continue

        edge_id = parse_edge_id_from_col(col)
        if edge_id is None:
            continue

        meta = edge_meta.get(
            edge_id,
            {"type": None, "oneway": True, "length (cm)": None}
        )

        traversals = {}

        day_series = day_df[col]

        # Iterate over time buckets (0..N-1)
        for idx, raw_val in enumerate(day_series):
            try:
                v = float(raw_val)
            except (TypeError, ValueError):
                v = float("nan")

            # Handle missing / invalid values
            if (v < 0) or math.isnan(v):
                if use_average_fallback and avg_df is not None:
                    try:
                        avg_v = float(avg_df[col].iloc[idx])
                    except (TypeError, ValueError, KeyError, IndexError):
                        avg_v = float("nan")

                    if (avg_v < 0) or math.isnan(avg_v):
                        # still invalid -> skip this time bucket
                        continue
                    v = avg_v
                else:
                    # no fallback, skip
                    continue

            # At this point v is valid
            traversals[str(idx)] = {
                "time to traverse (s)": float(v)
            }

        result[str(edge_id)] = {
            "type": meta["type"],
            "oneway": meta["oneway"],
            "length (cm)": meta["length (cm)"],
            "traversals": traversals,
        }

    return result

# -------------------------------
# Main
# -------------------------------

def main():
    base = DATA_DIR or "."

    vertex_path = os.path.join(base, VERTEX_FILE)
    edge_connections_path = os.path.join(base, EDGE_CONNECTIONS_FILE)
    averaged_path = os.path.join(base, AVERAGED_FILE)

    print("Building edge metadata (lengths)...")
    edge_meta = build_edge_metadata(vertex_path, edge_connections_path)
    print(f"  -> computed metadata for {len(edge_meta)} edges")

    avg_df = None
    if USE_AVERAGE_FALLBACK:
        print("Loading averaged traversal times...")
        avg_df = pd.read_csv(averaged_path)

    for day_file in DAY_FILES:
        day_path = os.path.join(base, day_file)
        print(f"Processing {day_file}...")

        day_df = pd.read_csv(day_path)
        day_json = build_json_for_day(day_df, avg_df, edge_meta, use_average_fallback=USE_AVERAGE_FALLBACK)

        # Output filename: edge_data_day3.csv -> edge_data_day3_formatted.json
        name, _ = os.path.splitext(day_file)
        out_name = f"{name}_formatted.json"
        out_path = os.path.join(base, out_name)

        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(day_json, f, indent=2)

        print(f"  -> wrote {out_name} with {len(day_json)} edges")

    print("Done.")

if __name__ == "__main__":
    main()
