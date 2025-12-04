from hashlib import new
import json
import random
import sys
from pathlib import Path

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

        # 1. Try Outward Edges (Unvisited)
        edges = node_data.get("outward_edges", [])       #Finding all of the edges we can take from that node.
        nodes = node_data.get("outward_vertices", [])    #Finding all of the nodes that each edge go to.

        available = []                      #Creating a list of available edges we can take.
        for e, v in zip(edges, nodes):      #Makes all the pairs (edge, node).
            if e not in visited_edges:      #Check if the edge is available (Not used in the route before).
                available.append((e, v))    #If available append to the list.
            
        if available:
            chosen_edge, next_node = random.choice(available)   #Picks a random of all available edges.
            sequence.append(chosen_edge)                        #Add the edge to the list.
            visited_edges.add(chosen_edge)                      #Add the edge to visited List.
            current_node = str(next_node)                       #Change the current node to the new noce.
        else:
            # 2. Backtracking (If no outward unvisited edges)
            # We allow visited edges here to ensure we don't get stuck.
            backward_edges = node_data.get("backward_edges", [])
            backward_nodes = node_data.get("backward_vertices", [])
            
            available_backward = []
            for e, v in zip(backward_edges, backward_nodes):
                available_backward.append((e, v))
            
            if available_backward:
                chosen_edge, next_node = random.choice(available_backward)
                sequence.append(chosen_edge)
                # We add to visited_edges so we don't immediately loop back if we treat it as a forward edge later,
                # but since we don't filter visited for backward edges, we can still traverse it back.
                visited_edges.add(chosen_edge)
                current_node = str(next_node)
            else:
                # 3. Dead End (No outward or backward edges)
                break

    return sequence #Return the sequence when we are done.

def GenerateRoutes(numberOfSequences, minLengthOfSequence, maxLengthOfSequence):
    InputFile = Path(__file__).parent.parent / "Data" / "LookupTableData" / "RoadNetwork.json"
    
    try:
        if not InputFile.is_file(): #Check if the file can be found, if not raise an error.
            raise FileNotFoundError(f'"RoadNetwork.json" does not exist. (Mising Dataset)')

        #Opens the file and load data into "GraphData".
        with open(InputFile, "r") as f:
            GraphData = json.load(f)

        Routes = []
        for _ in range(numberOfSequences):
            LengthOfSequence = random.randint(minLengthOfSequence, maxLengthOfSequence)
            sequence = random_sequence(GraphData, LengthOfSequence)
            if sequence:
                Routes.append(sequence)

        return Routes

    except Exception as e:
        raise RuntimeError(str(e))