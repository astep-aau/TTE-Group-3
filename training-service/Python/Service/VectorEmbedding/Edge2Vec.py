import json
import sys
import networkx as nx
from node2vec import Node2Vec
from pathlib import Path
import numpy as np
import tempfile
import shutil

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
        edge_to_id[(sourceNode, targetNode, edgeId)] = int(edge_data['edge'])   # Map edge tuple to edge ID (as int)

    edgeGraph = nx.relabel_nodes(edgeGraph, edge_to_id)                         # Relabel nodes in the line graph to use edge IDs
    return edgeGraph

def VectorEmbedding():
    temp_path = None
    try:
        # === File Paths ===
        InputFile = Path(__file__).parent.parent / "Data" / "LookupTableData" / "RoadNetwork.json"
        output_path = Path(__file__).parent.parent / "Data" / "LookupTableData" / "embeddings.npy"

        if not InputFile.is_file():
            raise FileNotFoundError(f'"{InputFile}" does not exist. (Missing Dataset)')

        print(f"📖 Loading graph from {InputFile}")
        try:
            with open(InputFile, "r") as f:
                Graph = json.load(f)
        except json.JSONDecodeError as e:
            raise ValueError(f"Invalid JSON in RoadNetwork.json: {e}")

        if not Graph:
            raise ValueError('"RoadNetwork.json" is empty or not loaded. (Empty Dataset)')

        print("🔨 Creating edge graph...")
        GraphEdge = createGraph(Graph)
        if not GraphEdge or len(GraphEdge.nodes()) == 0:
            raise ValueError('Graph not created correctly. (Empty Edge Graph)')

        print(f"📊 Graph has {len(GraphEdge.nodes())} edges")

        # Validate graph structure
        if not nx.is_weakly_connected(GraphEdge):
            print("⚠️  Warning: Graph is not fully connected - some edges may be isolated")

        print("🧠 Training Node2Vec model...")
        TrainedModel = modelTraining(GraphEdge)
        if not TrainedModel or not hasattr(TrainedModel, 'wv'):
            raise ValueError('Model training failed. (Invalid Trained Model)')

        # === Extract embeddings into NumPy array ===
        print("📦 Extracting embeddings...")

        # Get all edge IDs (sequential integers)
        edge_ids = sorted(GraphEdge.nodes())
        if not edge_ids:
            raise ValueError("No edge IDs found in graph")

        max_edge_id = max(edge_ids)

        # Validate sequential IDs
        if edge_ids != list(range(max_edge_id + 1)):
            raise ValueError(f"Edge IDs are not sequential! Expected 0-{max_edge_id}, got {edge_ids[:10]}...")

        # Create embeddings array (edge_id is the index)
        embedding_dim = TrainedModel.wv.vector_size
        embeddings = np.zeros((max_edge_id + 1, embedding_dim), dtype=np.float32)

        for edge_id in edge_ids:
            try:
                embeddings[edge_id] = TrainedModel.wv[edge_id]
            except KeyError:
                raise ValueError(f"Edge {edge_id} missing from trained model")

        # === Atomic write using temporary file ===
        print(f"💾 Writing embeddings to {output_path}...")

        # Ensure output directory exists
        output_path.parent.mkdir(parents=True, exist_ok=True)

        # Write to temporary file first
        try:
            with tempfile.NamedTemporaryFile(mode='wb', delete=False, dir=output_path.parent, suffix='.tmp') as tmp_file:
                temp_path = Path(tmp_file.name)
                np.save(tmp_file, embeddings)
        except OSError as e:
            raise IOError(f"Failed to write temporary file (disk full?): {e}")

        # Verify the file was written correctly
        if not temp_path.exists() or temp_path.stat().st_size == 0:
            raise IOError("Temporary file is empty or missing")

        # Atomically replace the old file
        try:
            shutil.move(str(temp_path), str(output_path))
            temp_path = None  # Successfully moved, no cleanup needed
        except OSError as e:
            raise IOError(f"Failed to move embeddings file: {e}")

        print(f"✅ Embeddings saved successfully!")
        print(f"   - Shape: {embeddings.shape} (edges × dimensions)")
        print(f"   - File: {output_path}")
        print(f"   - Size: {output_path.stat().st_size / 1024 / 1024:.2f} MB")

    except FileNotFoundError as e:
        print(f"❌ File Error: {e}", file=sys.stderr)
        sys.exit(1)
    except ValueError as e:
        print(f"❌ Validation Error: {e}", file=sys.stderr)
        sys.exit(1)
    except IOError as e:
        print(f"❌ I/O Error: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"❌ Unexpected Error: {e}", file=sys.stderr)
        sys.exit(1)
    finally:
        # Clean up temp file if it still exists (only if move failed)
        if temp_path and temp_path.exists():
            try:
                temp_path.unlink()
                print(f"🧹 Cleaned up temporary file: {temp_path}")
            except OSError:
                print(f"⚠️  Could not remove temporary file: {temp_path}", file=sys.stderr)