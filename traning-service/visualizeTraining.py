import matplotlib.pyplot as graph

def plotGraph(epochs, loss_history):
    graph.plot(range(1, epochs+1), loss_history, marker='o')
    graph.xlabel('Epoch')
    graph.ylabel('Loss')
    graph.title('Training Loss over Epochs')
    graph.show()