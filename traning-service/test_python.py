import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset
import matplotlib.pyplot as plt

# -----------------------------
# Define the LSTM model
class RouteTimeLSTM(nn.Module):

# -----------------------------
# Create dummy data & dataset
input_size = 3  # features: lon, lat, delta_time
model = RouteTimeLSTM(input_size=input_size)  # initialize model

# Random data: 10 sequences, each of length 5, 3 features per step
X = torch.rand((10, 5, input_size))  
y = torch.rand((10, 1))  # target travel time for each sequence

dataset = TensorDataset(X, y)  # combine features and targets
dataloader = DataLoader(dataset, batch_size=2, shuffle=True)  # batch loader

# -----------------------------
# Loss function and optimizer
criterion = nn.MSELoss()               # mean squared error for regression
optimizer = torch.optim.Adam(model.parameters(), lr=0.1)  # Adam optimizer

# -----------------------------
# Training loop
loss_history = []  # store average loss per epoch
epochs = 25        # number of training iterations

for epoch in range(epochs):
    epoch_loss = 0
    for batch_X, batch_y in dataloader:      # iterate over batches
        optimizer.zero_grad()                 # reset gradients
        outputs = model(batch_X)              # forward pass
        loss = criterion(outputs, batch_y)   # compute loss
        loss.backward()                       # backpropagate
        optimizer.step()                      # update weights
        epoch_loss += loss.item()             # accumulate loss

    avg_loss = epoch_loss / len(dataloader)   # average loss for this epoch
    loss_history.append(avg_loss)             # save for plotting
    print(f"Epoch {epoch+1}, Loss: {avg_loss:.4f}")  # print progress

# -----------------------------
# Save the trained model to disk
torch.save(model.state_dict(), "route_time_model.pht")
print("Training complete")

# -----------------------------
# Plot training loss over epochs
plt.plot(range(1, epochs+1), loss_history, marker='o')
plt.xlabel('Epoch')
plt.ylabel('Loss')
plt.title('Training Loss over Epochs')
plt.show()