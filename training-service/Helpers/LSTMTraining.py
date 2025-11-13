import matplotlib
matplotlib.use("Agg")  # Use non-interactive backend
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset, random_split
import numpy as np
import os
from pathlib import Path
from TrainingModel import LSTMModel
import matplotlib.pyplot as plt
import json

def TrainLSTMModel():
    # -----------------------------
    # 1️⃣ Load and preprocess data
    # -----------------------------
    json_path = Path(__file__).parent / "Datasets" / "TrainingSet.JSON"
    with open(json_path, "r") as f:
        data = json.load(f)

    X, y = [], []
    for seq in data["Sequences"]:
        X.append(seq["Edges"])
        y.append(seq.get("TotalTime", 0))

    max_len = max(len(seq) for seq in X)
    num_features = len(X[0][0])
    X_padded = np.zeros((len(X), max_len, num_features), dtype=np.float32)
    for i, seq in enumerate(X):
        for j, edge_vector in enumerate(seq):
            X_padded[i, j, :] = edge_vector

    X = torch.tensor(X_padded)
    y = torch.tensor(y, dtype=torch.float32).unsqueeze(1)
    dataset = TensorDataset(X, y)

    # Train/val/test split
    total_size = len(dataset)
    test_size = int(0.2 * total_size)
    train_val_size = total_size - test_size
    val_size = int(0.1 * total_size)
    train_size = train_val_size - val_size
    train_dataset, val_dataset, test_dataset = random_split(dataset, [train_size, val_size, test_size])

    train_loader = DataLoader(train_dataset, batch_size=20, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=100)

    # Normalization
    y_train = torch.stack([yb for _, yb in train_dataset])
    mean_y = y_train.mean()
    std_y = y_train.std()
    def normalize_targets(y): return (y - mean_y) / std_y

    # -----------------------------
    # 2️⃣ Model, loss, optimizer
    # -----------------------------
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = LSTMModel(input_size=num_features).to(device)
    criterion = nn.L1Loss()
    optimizer = torch.optim.Adam(model.parameters(), lr=0.001)

    # -----------------------------
    # 3️⃣ Training loop
    # -----------------------------
    num_epochs = 500
    patience = 5
    best_val_loss = float("inf")
    epochs_no_improve = 0
    train_losses, val_losses = [], []

    output_dir = Path(__file__).parent / "Datasets"
    os.makedirs(output_dir, exist_ok=True)

    for epoch in range(num_epochs):
        # Training
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

        # Validation
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

        # Early stopping & save best
        if val_loss < best_val_loss:
            best_val_loss = val_loss
            epochs_no_improve = 0
            normalization_dict = {"mean": mean_y, "std": std_y}
            torch.save({"state_dict": model.state_dict(), "normalization": normalization_dict},
            os.path.join(output_dir, "best_model.pt"))
        else:
            epochs_no_improve += 1
            if epochs_no_improve >= patience:
                print(f"Early stopping triggered at epoch {epoch+1}")
                break

    # Plot training loss
    plt.plot(train_losses, label="Train Loss")
    plt.plot(val_losses, label="Validation Loss")
    plt.xlabel("Epoch")
    plt.ylabel("MAE")
    plt.legend()
    plt.savefig(os.path.join(output_dir, "training_loss.png"))
    plt.close()
    print("Training done, best model saved.")