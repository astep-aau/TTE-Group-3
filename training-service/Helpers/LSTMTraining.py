import json
import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset, random_split
import matplotlib.pyplot as plt
import os

# 1️⃣ Load JSON data
json_path = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/TrainingSet.JSON"
with open(json_path, "r") as f:
    data = json.load(f)

# 2️⃣ Extract edge sequences and target times
X = []
y = []

for seq in data["Sequences"]:
    edges = seq["Edges"]
    total_time = seq.get("TotalTime", 0)
    X.append(edges)
    y.append(total_time)

# Determine max sequence length and feature size
max_len = max(len(seq) for seq in X)
num_features = len(X[0][0])

# Manually pad sequences with zeros
X_padded = np.zeros((len(X), max_len, num_features), dtype=np.float32)
for i, seq in enumerate(X):
    for j, edge_vector in enumerate(seq):
        X_padded[i, j, :] = edge_vector

X = torch.tensor(X_padded)
y = torch.tensor(y, dtype=torch.float32).unsqueeze(1)  # shape (N, 1)

# 3️⃣ Train/test split
dataset = TensorDataset(X, y)
test_size = int(0.2 * len(dataset))
train_size = len(dataset) - test_size
train_dataset, test_dataset = random_split(dataset, [train_size, test_size])

train_loader = DataLoader(train_dataset, batch_size=16, shuffle=True)
test_loader = DataLoader(test_dataset, batch_size=16)

# 4️⃣ Define LSTM model
class LSTMModel(nn.Module):
    def __init__(self, input_size, hidden_size=32):
        super().__init__()
        self.lstm = nn.LSTM(input_size, hidden_size, batch_first=True)
        self.fc1 = nn.Linear(hidden_size, 32)
        self.relu = nn.ReLU()
        self.fc2 = nn.Linear(32, 1)

    def forward(self, x):
        _, (h_n, _) = self.lstm(x)  # h_n shape: (1, batch, hidden_size)
        h_n = h_n.squeeze(0)
        out = self.fc1(h_n)
        out = self.relu(out)
        out = self.fc2(out)
        return out

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
model = LSTMModel(input_size=num_features).to(device)

criterion = nn.MSELoss()
optimizer = torch.optim.Adam(model.parameters(), lr=0.0001)

# 5️⃣ Training loop
num_epochs = 50
train_losses, val_losses = [], []

for epoch in range(num_epochs):
    model.train()
    epoch_loss = 0
    for xb, yb in train_loader:
        xb, yb = xb.to(device), yb.to(device)
        optimizer.zero_grad()
        out = model(xb)
        loss = criterion(out, yb)
        loss.backward()
        optimizer.step()
        epoch_loss += loss.item() * xb.size(0)
    epoch_loss /= len(train_loader.dataset)
    train_losses.append(epoch_loss)

    # Validation
    model.eval()
    val_loss = 0
    with torch.no_grad():
        for xb, yb in test_loader:
            xb, yb = xb.to(device), yb.to(device)
            out = model(xb)
            loss = criterion(out, yb)
            val_loss += loss.item() * xb.size(0)
    val_loss /= len(test_loader.dataset)
    val_losses.append(val_loss)

    print(f"Epoch {epoch+1}/{num_epochs}, Train Loss: {epoch_loss:.4f}, Val Loss: {val_loss:.4f}")

# 6️⃣ Evaluate on test set
model.eval()
test_loss = 0
predictions = []
actuals = []
with torch.no_grad():
    for xb, yb in test_loader:
        xb, yb = xb.to(device), yb.to(device)
        out = model(xb)
        test_loss += criterion(out, yb).item() * xb.size(0)
        predictions.append(out.cpu().numpy())
        actuals.append(yb.cpu().numpy())

test_loss /= len(test_loader.dataset)
predictions = np.vstack(predictions).flatten()
actuals = np.vstack(actuals).flatten()
print(f"Test MSE: {test_loss:.4f}")
print("Predictions:", predictions[:5])
print("Actual:", actuals[:5])

# 7️⃣ Plot training and validation loss
output_dir = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets"
os.makedirs(output_dir, exist_ok=True)
plot_path = os.path.join(output_dir, "training_loss.png")

plt.plot(train_losses, label="Train Loss")
plt.plot(val_losses, label="Validation Loss")
plt.xlabel("Epochs")
plt.ylabel("MSE")
plt.title("Training Loss over Time")
plt.legend()
plt.savefig(plot_path)
plt.close()

print(f"Training loss plot saved to: {plot_path}")