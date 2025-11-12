import json
import random
import sys
from pathlib import Path

# === Start Parameters ===
NumberOfSequences = 10
LengthOfSequence = 1
InputFile = Path(__file__).parent / "Datasets" / "RoadNetwork.json" 

# === Function to creates routes ===
def random_sequence(graph, lengthOfSequence):
    if not graph: #Check if the data is empty, if it is raise an error.
        raise ValueError('"RoadNetwork.json" is empty or not loaded. (Empty Dataset)')

    current_node = random.choice(list(graph.keys()))    #Find the current node, takes a random from the dataset.
    sequence = []                                       #List of edges that form the route.
    visited_edges = set()                               #List of visited edges.

    #Making a route 
    for _ in range(lengthOfSequence):       #Loops over lengthOfSequence times.
        node_data = graph.get(current_node) #Take the node in our graph we want to use.
        if not node_data:                   #Check if the node exist.
            break

        edges = node_data.get("outward_edges", [])       #Finding all of the edges we can take from that node.
        nodes = node_data.get("outward_vertices", [])    #Finding all of the nodes that each edge go to.

            
        available = []                      #Creating a list of available edges we can take.
        for e, v in zip(edges, nodes):      #Makes all the pairs (edge, node).
            if e not in visited_edges:      #Check if the edge is available (Not used in the route before).
                available.append((e, v))    #If available append to the list.
            
        if not available:   #If there is no availbe edges, stop it here.
            break

        chosen_edge, next_node = random.choice(available)   #Picks a random of all available edges.
        sequence.append(chosen_edge)                        #Add the edge to the list.
        visited_edges.add(chosen_edge)                      #Add the edge to visited List.
        current_node = str(next_node)                       #Change the current node to the new noce.

    print(sequence, file=sys.stderr, flush=True)
    return sequence #Return the sequence when we are done.

try:
    if not InputFile.is_file(): #Check if the file can be found, if not raise an error.
        raise FileNotFoundError(f'"RoadNetwork.json" does not exist. (Mising Dataset)')

    #Opens the file and load data into "GraphData".
    with open(InputFile, "r") as f:
        GraphData = json.load(f)

    Routes = []
    for _ in range(NumberOfSequences):
        LengthOfSequence = random.randint(1, 1)
        sequence = random_sequence(GraphData, LengthOfSequence)
        if sequence:
            Routes.append(sequence)

    print(json.dumps(Routes))

except Exception as e:
    print(str(e), file=sys.stderr)
    sys.exit(1)