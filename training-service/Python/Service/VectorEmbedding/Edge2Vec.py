import json
import sys
import networkx as nx
from node2vec import Node2Vec
from pathlib import Path
import sqlite3

# === Function to train Node2Vec model ===
def modelTraining(GraphForEdges):
    node2vec = Node2Vec(    # Initialize Node2Vec model
        GraphForEdges,      # The graph
        dimensions=32,      # Embedding dimensions
        walk_length=150,    # Length of each random walk
        num_walks=100,      # Number of walks per node
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
        # === File Paths ===
        db_path = Path(__file__).parent.parent / "Data" / "data.db"
        InputFile = Path(__file__).parent.parent / "Data" / "RoadNetwork.json"
        
        if not InputFile.is_file():
            raise FileNotFoundError(f'"{InputFile}" does not exist. (Missing Dataset)')
    
        with open(InputFile, "r") as f:
            Graph = json.load(f)

        if not Graph:
            raise ValueError('"RoadNetwork.json" is empty or not loaded. (Empty Dataset)')

        GraphEdge = createGraph(Graph)
        if not GraphEdge:
            raise ValueError('Graph not created correctly. (Empty Edge Graph)')

        TrainedModel = modelTraining(GraphEdge)
        if not TrainedModel:
            raise ValueError('Model training failed. (Empty Trained Model)')

        # === Write embeddings to database ===
        conn = sqlite3.connect(db_path)
        cur = conn.cursor()
        
        try:
            # Create embeddings table if it doesn't exist
            cur.execute("""
            CREATE TABLE IF NOT EXISTS embeddings (
                edge_id TEXT PRIMARY KEY,
                vector TEXT NOT NULL
            );
            """)
            
            print("Clearing existing embeddings...")
            cur.execute("DELETE FROM embeddings")
            
            print("Inserting embeddings...")
            insert_count = 0
            for edge in GraphEdge.nodes():
                vector = TrainedModel.wv[edge].tolist()
                vector_json = json.dumps(vector)
                cur.execute("""
                    INSERT INTO embeddings (edge_id, vector)
                    VALUES (?, ?)
                """, (edge, vector_json))
                insert_count += 1
            
            # Create index for performance
            print("Creating index...")
            cur.execute("CREATE INDEX IF NOT EXISTS idx_edge_id ON embeddings(edge_id)")
            
            conn.commit()
            
            print(f"✅ Embeddings saved to database → {db_path}")
            print(f"   - Embeddings inserted: {insert_count} vectors")
            
        except Exception as e:
            conn.rollback()
            print(f"❌ Database error (rolled back): {e}", file=sys.stderr)
            raise
            
        finally:
            cur.close()
            conn.close()
            
    except Exception as e:
        print(str(e), file=sys.stderr)
        sys.exit(1)