import json
import sys
import networkx as nx
from node2vec import Node2Vec
from pathlib import Path

# === Function to train Node2Vec model ===
def modelTraining(GraphForEdges):
    node2vec = Node2Vec(    # Initialize Node2Vec model
        GraphForEdges,      # The graph
        dimensions=5,       # Embedding dimensions
        walk_length=15,     # Length of each random walk
        num_walks=10,       # Number of walks per node
        p=1,                # Return hyperparameter
        q=1,                # Input hyperparameter
        workers=4           # Number of parallel workers
    )
    model = node2vec.fit(window=10, min_count=1, batch_words=4) # Train the model
    return model 

# === Function to create Graph of the edges ===
def createGraph(Graph):                                                  
    mGraph = nx.MultiDiGraph()                                          # Initialize a directed multigraph
    for sourceNode, info in Graph.items():                              # Iterate through each node in the original graph  
        sourceId = int(sourceNode)                                      # Convert edge ID to integer
        edges = info.get("outward_edges", [])                           # Get outward edges
        targets = info.get("outward_vertices", [])                      # Get outward vertices
        for edge, target in zip(edges, targets):                        # Add edges to the multigraph
            targetId = int(target)                                     # Convert target node ID to integer
            mGraph.add_edge(sourceId, targetId, key=edge, edge=edge)   # Add edge with edge ID as key

    edgeGraph = nx.line_graph(mGraph)                               # Create line graph (edges as nodes)   

    edge_to_id = {}                                                           
    for sourceNode, targetNode, edgeId in edgeGraph.nodes():                    # Iterate through each edge in the line graph
        edge_data = mGraph.get_edge_data(sourceNode, targetNode, edgeId)        # Get edge data from the original multigraph
        edge_to_id[(sourceNode, targetNode, edgeId)] = str(edge_data['edge'])   # Map edge tuple to edge ID

    edgeGraph = nx.relabel_nodes(edgeGraph, edge_to_id)                         # Relabel nodes in the line graph to use edge IDs
    return edgeGraph

def VectorEmbedding():
    try:
        # Initialize embeddings dictionary
        embeddingsDict = {}    

        # === File Paths ===
        OutputFile = Path(__file__).parent / "Datasets" / "edgeEmbeddings.json"
        InputFile = Path(__file__).parent / "Datasets" / "RoadNetwork.json"
        if not InputFile.is_file(): #Check if the file can be found, if not raise an error.
            raise FileNotFoundError(f'"{InputFile}" does not exist. (Missing Dataset)')
    
        with open(InputFile, "r") as f:
            Graph = json.load(f)

        if not Graph: #Check if the data is empty, if it is raise an error.
            raise ValueError('"RoadNetwork.json" is empty or not loaded. (Empty Dataset)')

        GraphEdge = createGraph(Graph)               # Create graph of edges
        if not GraphEdge: #Check if the data is empty, if it is raise an error.
            raise ValueError('Graph not created correctly. (Empty Edge Graph)')

        TrainedModel = modelTraining(GraphEdge) # Train Node2Vec model
        if not TrainedModel: #Check if the data is empty, if it is raise an error.
            raise ValueError('Model training failed. (Empty Trained Model)')

        for edge in GraphEdge.nodes():
            embeddingsDict[edge] = TrainedModel.wv[edge].tolist()

        with open(OutputFile, "w") as f:
            json.dump(embeddingsDict, f, indent=2)

        if not OutputFile.is_file(): #Check if the file can be found, if not raise an error.
            raise FileNotFoundError(f'"{OutputFile}" does not exist. (Missing Dataset)')
    except Exception as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)