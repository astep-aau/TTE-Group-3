import sys
from .embeddings_cache import get_embeddings
from fastapi import HTTPException
import math

# ===Function that converts a list of edges to their vectors===
def convertEdgeToVector(embeddings, edges, timeBucket=None):
    """Converts a list of edges to their vector representations with optional time encoding.
    
    Args:
        embeddings: Dictionary mapping edge IDs to their embedding vectors
        edges: List of edge IDs to convert
        timeBucket: Optional time bucket (0-287) representing 5-minute intervals in a day.
                   If provided, appends sinusoidal time encoding (sin, cos) to each vector.
    
    Returns:
        List of vectors (with time encoding if timeBucket is provided)
    """
    
    vectors = []                                                            #List of vectors to return    
    for edge in edges:                                                      #Loop over all edges  
        vector = embeddings.get(str(edge))                                       #Get the vector for the edge
        if vector is None:                                                  #Check if the vector is missing
            raise ValueError('No vector for that Edge. (Missing values)')   
        
        # If timeBucket is provided, append sinusoidal encoding
        if timeBucket is not None:
            # Normalize time bucket (0-287) to 0-2pi
            # 288 buckets in a day (5 min intervals)
            time_angle = 2 * math.pi * timeBucket / 288.0
            sin_time = math.sin(time_angle)
            cos_time = math.cos(time_angle)
            
            # Create a new list to avoid modifying the cached vector
            vector = list(vector) + [sin_time, cos_time]
            
        vectors.append(vector)                                              #Add the vector to the list
    return vectors

def GetEdgeToVectors(edges, timeBucket=None):
    if timeBucket is not None and (timeBucket < 0 or timeBucket > 287):
        raise HTTPException(status_code=400, detail=f"timeBucket must be between 0 and 287, got {timeBucket}")
    try:
        if not edges: #Check if the data is empty, if it is raise an error.
            raise ValueError('No route data provided. (Empty Route)')

        # Use cached embeddings instead of loading from file every time
        Embeddings = get_embeddings()

        #Call the function that converts edges to their vectors
        vectors = convertEdgeToVector(Embeddings, edges, timeBucket)

        if not vectors: #Check if the data is empty, if it is raise an error.
            raise ValueError('No Vectors Converted. (Error in Conversion)')
    
        return vectors
    except Exception as e:
        raise RuntimeError(str(e))