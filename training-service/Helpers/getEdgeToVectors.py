import sys
import json
from pathlib import Path

# ===Function that converts a list of edges to their vectors===
def convertEdgeToVector(embeddings, edges):
    vectors = []                                                            #List of vectors to return    
    for edge in edges:                                                      #Loop over all edges  
        vector = embeddings.get(edge)                                       #Get the vector for the edge
        if vector is None:                                                  #Check if the vector is missing
            raise ValueError('No vector for that Edge. (Missing values)')   
        vectors.append(vector)                                              #Add the vector to the list
    return vectors

#Runtime Code
try:
    InputFile = Path(__file__).parent / "Datasets" / "edgeEmbeddings.emb"   #Path to the embeddings file
    edges = json.loads(sys.argv[1])                                         #List of edges to convert

    if not edges: #Check if the data is empty, if it is raise an error.
        raise ValueError('No route data provided. (Empty Route)')
    
    if not InputFile.is_file(): #Check if the file can be found, if not raise an error. 
        raise FileNotFoundError(f'"edgeEmbeddings.emb" does not exist. (Missing dataset)')
    
    with open(InputFile, "r") as f:
        Embeddings = json.load(f)

    #Call the function that converts edges to their vectors
    vectors = convertEdgeToVector(Embeddings, edges)

    if not vectors: #Check if the data is empty, if it is raise an error.
        raise ValueError('No Vectors Converted. (Error in Conversion)')
    
    print(json.dumps(vectors))
except Exception as e:
    print(str(e), file=sys.stderr)
    sys.exit(1)