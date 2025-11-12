import json
import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset, random_split
import matplotlib.pyplot as plt
import os
from pathlib import Path

# -----------------------------
# 1️⃣ Load JSON data
# -----------------------------
json_path = Path(__file__).parent / "Datasets" / "TrainingSet.JSON" 
with open(json_path, "r") as f:
    data = json.load(f)

# -----------------------------
# 2️⃣ Extract edge sequences and target times
# -----------------------------
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

# Pad sequences with zeros
X_padded = np.zeros((len(X), max_len, num_features), dtype=np.float32)
for i, seq in enumerate(X):
    for j, edge_vector in enumerate(seq):
        X_padded[i, j, :] = edge_vector

X = torch.tensor(X_padded)
y = torch.tensor(y, dtype=torch.float32).unsqueeze(1)  # shape (N,1)

# -----------------------------
# 3️⃣ Create TensorDataset
# -----------------------------
dataset = TensorDataset(X, y)

# Split: 70% train, 10% validation, 20% test
total_size = len(dataset)
test_size = int(0.2 * total_size)
train_val_size = total_size - test_size
val_size = int(0.1 * total_size)
train_size = train_val_size - val_size

train_dataset, val_dataset, test_dataset = random_split(dataset, [train_size, val_size, test_size])

train_loader = DataLoader(train_dataset, batch_size=20, shuffle=True)
val_loader = DataLoader(val_dataset, batch_size=100)
test_loader = DataLoader(test_dataset, batch_size=100)

# -----------------------------
# 4️⃣ Compute normalization stats on training set
# -----------------------------
y_train = torch.stack([yb for _, yb in train_dataset])
mean_y = y_train.mean()
std_y = y_train.std()

def normalize_targets(y):
    return (y - mean_y) / std_y

# -----------------------------
# 5️⃣ Define LSTM model
# -----------------------------
class LSTMModel(nn.Module):
    def __init__(self, input_size, hidden_size=64, dropout=0.2):
        super().__init__()
        self.lstm = nn.LSTM(input_size, hidden_size, batch_first=True)

        # Linear layers
        self.fc = nn.Linear(hidden_size, 16)
        self.fc1 = nn.Linear(16, 8)
        self.fc2 = nn.Linear(8, 1)

        # Non-linearity and regularization
        self.relu = nn.ReLU()
        self.dropout = nn.Dropout(dropout)

    def forward(self, x):
        _, (h_n, _) = self.lstm(x)
        h_n = h_n.squeeze(0)

        out = self.fc(h_n)
        out = self.relu(out)
        out = self.dropout(out)

        out = self.fc1(out)
        out = self.relu(out)
        out = self.dropout(out)

        out = self.fc2(out)  # final output, no ReLU here
        return out

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
model = LSTMModel(input_size=num_features).to(device)
# -----------------------------
# 6️⃣ Loss and optimizer
# -----------------------------
criterion = nn.L1Loss()  # MAE
optimizer = torch.optim.Adam(model.parameters(), lr=0.0001)

# -----------------------------
# 7️⃣ Training loop with early stopping
# -----------------------------
num_epochs = 500
patience = 5
best_val_loss = float('inf')
epochs_no_improve = 0

train_losses, val_losses = [], []

output_dir = Path(__file__).parent / "Datasets"
os.makedirs(output_dir, exist_ok=True)

for epoch in range(num_epochs):
    # ----- Training -----
    model.train()
    epoch_loss = 0
    for xb, yb in train_loader:
        xb, yb = xb.to(device), yb.to(device)
        yb_norm = normalize_targets(yb)
        optimizer.zero_grad()
        out = model(xb)
        loss = criterion(out, yb_norm)
        loss.backward()
        optimizer.step()
        epoch_loss += loss.item() * xb.size(0)
    epoch_loss /= len(train_loader.dataset)
    train_losses.append(epoch_loss)

    # ----- Validation -----
    model.eval()
    val_loss = 0
    with torch.no_grad():
        for xb, yb in val_loader:
            xb, yb = xb.to(device), yb.to(device)
            out_norm = model(xb)
            out_seconds = out_norm * std_y + mean_y
            val_loss += criterion(out_seconds, yb) * xb.size(0)
    val_loss /= len(val_loader.dataset)
    val_losses.append(val_loss.item())

    print(f"Epoch {epoch+1}/{num_epochs}, Train Loss: {epoch_loss:.4f}, Val Loss: {val_loss:.4f}")

    # ----- Early Stopping -----
    if val_loss < best_val_loss:
        best_val_loss = val_loss
        epochs_no_improve = 0
        torch.save(model.state_dict(), os.path.join(output_dir, "best_model.pt"))
    else:
        epochs_no_improve += 1
        if epochs_no_improve >= patience:
            print(f"Early stopping triggered at epoch {epoch+1}.")
            break

# -----------------------------
# 8️⃣ Evaluate on test set
# -----------------------------
model.eval()
predictions = []
actuals = []
with torch.no_grad():
    for xb, yb in test_loader:
        xb, yb = xb.to(device), yb.to(device)
        out_norm = model(xb)
        out_seconds = out_norm * std_y + mean_y
        predictions.append(out_seconds.cpu().numpy())
        actuals.append(yb.cpu().numpy())

predictions = np.vstack(predictions).flatten()
actuals = np.vstack(actuals).flatten()
test_loss = np.mean(np.abs(predictions - actuals))

print(f"Test MAE (seconds): {test_loss:.4f}")
print("Predictions:", predictions[:5])
print("Actuals:", actuals[:5])

# -----------------------------
# 9️⃣ Plot training and validation loss
# -----------------------------
plot_path = os.path.join(output_dir, "training_loss.png")
plt.plot(train_losses, label="Train Loss (normalized)")
plt.plot(val_losses, label="Validation Loss (seconds)")
plt.xlabel("Epochs")
plt.ylabel("MAE")
plt.title("Training Loss over Time")
plt.legend()
plt.savefig(plot_path)
plt.close()
print(f"Training loss plot saved to: {plot_path}")