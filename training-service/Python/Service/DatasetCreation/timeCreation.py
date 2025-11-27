import json
import random
from pathlib import Path
import numpy as np
from .embeddings_cache import get_embeddings

# === Function to calculate edge traversal times ===
def get_edge_time(route, traversalData, embeddings_dict, timeBucket=None):
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
        return []

    # Chose a random bucket from the available buckets
    if timeBucket is not None:
        chosen_bucket = timeBucket
    else:
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

    return times

def EdgeTraversalTime(route, timeBucket=None):
    InputFile = Path(__file__).parent.parent / "Data" / "RoadTraversal.json"
    Route = route

    try:
        if not InputFile.is_file(): #Check if the file can be found, if not raise an error.
            raise FileNotFoundError(f'"RoadTraversal.json" does not exist. (Mising Dataset)')
    
        if not Route: #Check if the data is empty, if it is raise an error.
            raise ValueError('No route data provided. (Empty Route)')

        #Opens the file and load data into "traversalData".
        with open(InputFile, "r") as f:
            traversalData = json.load(f)

        if not traversalData: #Check if travalsalData is empty, if it is raise an error.
            raise ValueError('"RoadTraversal.json" is empty or not loaded. (Empty Dataset)')
    
        # Use cached embeddings instead of loading from file every time
        embeddingData = get_embeddings()
    
        times = get_edge_time(Route, traversalData, embeddingData, timeBucket)
        return times
    except Exception as e:
        raise RuntimeError(str(e))