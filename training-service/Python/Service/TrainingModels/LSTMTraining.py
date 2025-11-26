import matplotlib
matplotlib.use("Agg")  # Use non-interactive backend
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset, random_split
import numpy as np
import os
from pathlib import Path
from .TrainingModel import LSTMModel
import matplotlib.pyplot as plt
import json
import datetime

def TrainLSTMModel(ModelName):
    # -----------------------------
    # 1️⃣ Load and preprocess data
    # -----------------------------
    json_path = Path(__file__).parent.parent / "Data" / "TrainingSet.JSON"
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

    train_loader = DataLoader(train_dataset, batch_size=64, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=128)

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
    criterion = nn.SmoothL1Loss()
    optimizer = torch.optim.Adam(model.parameters(), lr=0.002)

    # -----------------------------
    # 3️⃣ Training loop
    # -----------------------------
    num_epochs = 500
    patience = 10
    best_val_loss = float("inf")
    epochs_no_improve = 0
    train_losses, val_losses = [], []

    output_dir = Path(__file__).parent.parent / "Data" / "TrainedModels"
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
            os.makedirs(output_dir / f"{ModelName}Model" , exist_ok=True)
            torch.save({"state_dict": model.state_dict(), "normalization": normalization_dict},
            os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}.pt"))
        else:
            epochs_no_improve += 1
            if epochs_no_improve >= patience:
                print(f"Early stopping triggered at epoch {epoch+1}")
                break

    # Plot training loss
    plt.plot([loss * std_y + mean_y for loss in train_losses], label="Train Loss")
    plt.plot(val_losses, label="Validation Loss")
    plt.xlabel("Epoch")
    plt.ylabel("MAE")
    plt.legend()
    plt.savefig(os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}_TrainingData.png"))
    plt.close()

    # -----------------------------
    # 4️⃣ Test Evaluation & README
    # -----------------------------
    # Load best model state if available
    if best_val_loss < float("inf"):
        checkpoint = torch.load(os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}.pt"))
        model.load_state_dict(checkpoint["state_dict"])

    test_loader = DataLoader(test_dataset, batch_size=128)
    model.eval()
    test_loss = 0
    with torch.no_grad():
        for xb, yb in test_loader:
            xb, yb = xb.to(device), yb.to(device)
            out_norm = model(xb)
            out_seconds = out_norm * std_y + mean_y
            test_loss += criterion(out_seconds, yb) * xb.size(0)
    test_loss /= len(test_loader.dataset)

    print(f"Final Test Loss: {test_loss:.4f}")

    feedforward_layers = []
    for layer in model.fc_layers:
        cls_name = layer.__class__.__name__
        if isinstance(layer, nn.Linear):
            feedforward_layers.append(f"Linear({layer.in_features}->{layer.out_features})")
        elif isinstance(layer, nn.ReLU):
            feedforward_layers.append("ReLU")
        elif isinstance(layer, nn.Dropout):
            feedforward_layers.append(f"Dropout({layer.p})")

    feedforward_layers_str = " → ".join(feedforward_layers)
    dropout_value = [layer.p for layer in model.fc_layers if isinstance(layer, nn.Dropout)][0]

    # Generate README
    readme_path = output_dir / f"{ModelName}Model" / "README.md"

    readme_content = f"""# LSTM Model Training Results

**Date:** {datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}  
**Project:** {ModelName} - Route Time Estimation / Sequence Prediction  

---

## 1️⃣ Dataset Overview
| Property | Value |
|----------|-------|
| **Number of Samples (Train/Val/Test)** | {train_size} / {val_size} / {test_size} |
| **Route Length (Smallest/Largest)** | {(X.abs().sum(dim=2) != 0).sum(dim=1).min().item()} / {(X.abs().sum(dim=2) != 0).sum(dim=1).max().item()} |
| **Number of Input Features** | {num_features} |
| **Output** | 1 (Route Time in Seconds) |

---

## 2️⃣ Model Architecture
| Component | Configuration |
|-----------|---------------|
| **Model Type** | {type(model).__name__} |
| **Input Features** | {num_features} |
| **Hidden Size** | {model.lstm.hidden_size} |
| **Dropout** | {dropout_value} |
| **Feedforward Layers** | {feedforward_layers_str} |
| **Total Parameters** | {sum(p.numel() for p in model.parameters())} |

---

## 3️⃣ Training Configuration
| Property | Value |
|----------|-------|
| **Total Epochs** | {epoch + 1} |
| **Early Stopping** | {"Yes" if epochs_no_improve >= patience else "No"} |
| **Batch Size (Train / Val / Test)** | {train_loader.batch_size} / {val_loader.batch_size} / {test_loader.batch_size} |
| **Optimizer** | {type(optimizer).__name__} |
| **Learning Rate** | {optimizer.param_groups[0]['lr']} |
| **Loss Function** | {type(criterion).__name__} |

---

## 4️⃣ Performance Metrics
| Metric | Train | Validation | Test |
|--------|-------|------------|------|
| **MAE (Mean Absolute Error)** | {train_losses[-1] * std_y + mean_y:.2f} s | {val_loss:.2f} s | {test_loss:.2f} s |

---

## 5️⃣ Training Dynamics
- **Loss Progression:** see plot below (train vs. validation loss)

![Training Plot]({ModelName}_TrainingData.png)

---
"""

    with open(readme_path, "w") as f:
        f.write(readme_content)

    print(f"Training done. Results saved to {readme_path}")