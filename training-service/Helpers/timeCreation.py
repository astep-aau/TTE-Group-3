import json
import random
from pathlib import Path
import numpy as np

# === TEMPORARY: Simple cache for local dev (DELETE FOR K8S DEPLOYMENT) ===
_cache = {}

def _load_cached(file_path):
    """Load JSON with simple caching (TEMPORARY - remove for K8s)"""
    key = str(file_path)
    if key not in _cache:
        with open(file_path, "r") as f:
            _cache[key] = json.load(f)
    return _cache[key]
# === END TEMPORARY CACHE ===

# === Function to calculate edge traversal times ===
def get_edge_time(route, traversalData, embeddings_dict):
    #Compute the traversal times for each edge in the route based on traversal data.
    times = []                  #List of times for each edge in the route
    bucketsAvailable = set()    #Creates a empty set with all available buckets for the route

    for edgeId in route:                                           #This loop over each each in the route
        edge = str(edgeId)                                         #Convert the edge to string as our data is JSON
        if edge in traversalData:                                   #Check if the edge exist in the data
            edgeData = traversalData[edge]                          #Get the data for that edge
            edgeTraversals = edgeData.get("traversals", {})         #Get all traversals for that edge
            bucketKeys = edgeTraversals.keys()                      #Get all bucket keys for that edge
            bucketsAvailable.update(int(k) for k in bucketKeys)     #Add the buckets to the set of all buckets

    #If there is no available buckets, return empty list
    if not bucketsAvailable:
        return [], 0

    # Chose a random bucket from the available buckets
    chosen_bucket = random.choice(sorted(bucketsAvailable))

    # Compute the time of each edge with the chosen bucket
    for edgeId in route:                 #Loop over each edge in the route
        edge = str(edgeId)               #Convert the edge to string
          
        if edge not in traversalData:    #If the edge is not in the data, assign a default time of 0.0 seconds
            times.append(0.0)               
            continue
        
        traversals = traversalData[edge].get("traversals", {})  #Get all traversals for that edge
        if not traversals:                                     #If there is no data asign default 5.0 seconds 
            vector = np.array(embeddings_dict[str(edgeId)], dtype=np.float32)
            closest_edge = []
            for other_edge_id, other_vector_list in embeddings_dict.items():
                if other_edge_id == str(edgeId):
                    continue  # skip itself
                other_vector = np.array(other_vector_list, dtype=np.float32)
                distance = np.linalg.norm(vector - other_vector)  # Euclidean distance
                closest_edge.append((other_edge_id, distance))
            closest_edge.sort(key=lambda x: x[1])
            for other_edge_id, _ in closest_edge:
                traversals = traversalData.get(other_edge_id, {}).get("traversals", {})
                if traversals:
                    break
            if not traversals:
                times.append(5.0)
                continue

        bucket_keys = sorted(int(k) for k in traversals.keys())                 #Finding all bucket keys for that edge
        closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket)) #Find the key closest to the chosen bucket
        times.append(traversals[str(closest_bucket)]["time to traverse (s)"])   #Append the time to the list of times

    return times, chosen_bucket

def EdgeTraversalTime(route, day_number=0):
    datasets_dir = Path(__file__).parent / "Datasets"
    
    # Always load averaged data as fallback
    averaged_file = datasets_dir / "RoadTraversal.json"
    
    # Load day-specific file if day_number is provided
    if day_number > 0:
        day_file = datasets_dir / f"edge_data_day{day_number}_formatted.json"
        print(f"Using day-specific data: {day_file.name} with averaged fallback (cached)")
    else:
        day_file = None
        print("Using averaged traversal data only: RoadTraversal.json (cached)")
    
    EmbeddingFile = datasets_dir / "edgeEmbeddings.json"
    Route = route

    try:
        # Load averaged data
        if not averaged_file.is_file():
            raise FileNotFoundError(f'"RoadTraversal.json" does not exist. (Missing Dataset)')
        
        # TEMP: Using cache for local dev (DELETE FOR K8S)
        averaged_data = _load_cached(averaged_file)
        # K8s: Uncomment below and delete cache line above
        # with open(averaged_file, "r") as f:
        #     averaged_data = json.load(f)
        
        # Load day-specific data if requested
        day_data = None
        if day_file and day_file.is_file():
            # TEMP: Using cache for local dev (DELETE FOR K8S)
            day_data = _load_cached(day_file)
            # K8s: Uncomment below and delete cache line above
            # with open(day_file, "r") as f:
            #     day_data = json.load(f)
        elif day_file:
            print(f"Warning: {day_file.name} not found, using averaged data only")
    
        if not EmbeddingFile.is_file():
            raise FileNotFoundError(f'"edgeEmbeddings.json" does not exist. (Missing Dataset)')

        if not Route:
            raise ValueError('No route data provided. (Empty Route)')
    
        # Load embeddings
        # TEMP: Using cache for local dev (DELETE FOR K8S)
        embeddingData = _load_cached(EmbeddingFile)
        # K8s: Uncomment below and delete cache line above
        # with open(EmbeddingFile, "r") as f:
        #     embeddingData = json.load(f)

        if not embeddingData:
            raise ValueError('"edgeEmbeddings.json" is empty or not loaded. (Empty Dataset)')    
    
        # Calculate times using day-specific data with averaged fallback
        times, bucket = get_edge_time_with_fallback(Route, day_data, averaged_data, embeddingData)
        return {"times": times, "bucket": bucket}
    except Exception as e:
        raise RuntimeError(str(e))

def get_edge_time_with_fallback(route, day_data, averaged_data, embeddings_dict):
    """
    Calculate edge times using day-specific data first, falling back to averaged data.
    """
    times = []
    bucketsAvailable = set()

    # Collect available buckets from BOTH datasets
    for edgeId in route:
        edge = str(edgeId)
        
        # Check day-specific data first
        if day_data and edge in day_data:
            edgeTraversals = day_data[edge].get("traversals", {})
            bucketKeys = edgeTraversals.keys()
            bucketsAvailable.update(int(k) for k in bucketKeys)
        
        # Also check averaged data
        if edge in averaged_data:
            edgeTraversals = averaged_data[edge].get("traversals", {})
            bucketKeys = edgeTraversals.keys()
            bucketsAvailable.update(int(k) for k in bucketKeys)

    if not bucketsAvailable:
        return [], 0

    # Choose a random bucket from available buckets
    chosen_bucket = random.choice(sorted(bucketsAvailable))

    # Calculate time for each edge
    for edgeId in route:
        edge = str(edgeId)
        time_found = False
        
        # Try day-specific data first
        if day_data and edge in day_data:
            traversals = day_data[edge].get("traversals", {})
            if traversals:
                bucket_keys = sorted(int(k) for k in traversals.keys())
                closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                times.append(traversals[str(closest_bucket)]["time to traverse (s)"])
                time_found = True
        
        # Fall back to averaged data if not found in day-specific
        if not time_found and edge in averaged_data:
            traversals = averaged_data[edge].get("traversals", {})
            if traversals:
                bucket_keys = sorted(int(k) for k in traversals.keys())
                closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                times.append(traversals[str(closest_bucket)]["time to traverse (s)"])
                time_found = True
        
        # Last resort: use embedding similarity
        if not time_found:
            vector = np.array(embeddings_dict.get(str(edgeId), [0]*16), dtype=np.float32)
            closest_edge = []
            for other_edge_id, other_vector_list in embeddings_dict.items():
                if other_edge_id == str(edgeId):
                    continue
                other_vector = np.array(other_vector_list, dtype=np.float32)
                distance = np.linalg.norm(vector - other_vector)
                closest_edge.append((other_edge_id, distance))
            
            closest_edge.sort(key=lambda x: x[1])
            for other_edge_id, _ in closest_edge[:5]:  # Try top 5 similar edges
                # Check averaged data for similar edge
                traversals = averaged_data.get(other_edge_id, {}).get("traversals", {})
                if traversals:
                    bucket_keys = sorted(int(k) for k in traversals.keys())
                    closest_bucket = min(bucket_keys, key=lambda k: abs(k - chosen_bucket))
                    times.append(traversals[str(closest_bucket)]["time to traverse (s)"])
                    time_found = True
                    break
            
            if not time_found:
                times.append(0.0)  # Ultimate fallback

    return times, chosen_bucket