import sys
from .embeddings_cache import get_embeddings

# ===Function that converts a list of edges to their vectors===
def convertEdgeToVector(embeddings, edges):
    vectors = []                                                            #List of vectors to return    
    for edge in edges:                                                      #Loop over all edges  
        vector = embeddings.get(str(edge))                                       #Get the vector for the edge
        if vector is None:                                                  #Check if the vector is missing
            raise ValueError('No vector for that Edge. (Missing values)')   
        vectors.append(vector)                                              #Add the vector to the list
    return vectors

def GetEdgeToVectors(edges):
    try:
        if not edges: #Check if the data is empty, if it is raise an error.
            raise ValueError('No route data provided. (Empty Route)')

        # Use cached embeddings instead of loading from file every time
        Embeddings = get_embeddings()

        #Call the function that converts edges to their vectors
        vectors = convertEdgeToVector(Embeddings, edges)

        if not vectors: #Check if the data is empty, if it is raise an error.
            raise ValueError('No Vectors Converted. (Error in Conversion)')
    
        return vectors
    except Exception as e:
        raise RuntimeError(str(e))