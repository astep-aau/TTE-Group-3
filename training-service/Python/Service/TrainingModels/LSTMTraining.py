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
import sqlite3

def GenerateFigure(plot_infos, output_path):
    fig, axes = plt.subplots(2, 2, figsize=(12, 10))
    axes = axes.flatten()

    for i, info in enumerate(plot_infos):
        ax = axes[i]
        if info["type"] == "line":
            xs = info["x"]
            ys = info["y"]
            labels = info.get("label", [None]*len(xs))
            # Ensure each series is plotted
            for x_series, y_series, label in zip(xs, ys, labels):
                ax.plot(x_series, y_series, label=label)
            if any(labels):
                ax.legend()
        elif info["type"] == "bar":
            ax.bar(info["x"], info["y"], width=info.get("width", 0.8), edgecolor='black')
        elif info["type"] == "hist":
            ax.hist(info["x"], bins=info["bins"], edgecolor='black')
            for b in info["bins"]:
                ax.axvline(b, linestyle='--', alpha=0.3, color='gray')

        ax.set_xlabel(info.get("xlabel", ""))
        ax.set_ylabel(info.get("ylabel", ""))
        ax.set_title(info.get("title", ""))

    # Turn off empty subplots
    for j in range(len(plot_infos), 4):
        axes[j].axis('off')

    plt.tight_layout()
    fig.savefig(output_path)
    plt.close(fig)


def TrainLSTMModel(ModelName):
    # Connect to database
    db_path = Path(__file__).parent.parent / "Data" / "data.db"
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

# Fetch all vectors and their lengths
    cursor.execute("SELECT VECTOR, LENGTH_CM FROM embeddings")
    rows = cursor.fetchall()

# VECTOR might be stored as a string or binary; convert to tuple/list if needed
    vector_to_length = {}
    for vec, length in rows:
        # Example if VECTOR stored as string '[1.0, 2.0, ...]'
        vec_tuple = tuple(map(float, vec.strip("[]").split(",")))
        vector_to_length[vec_tuple] = length    
    conn.close()

    json_path = Path(__file__).parent.parent / "Data" / "TrainingSet.json"
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

    train_loader = DataLoader(train_dataset, batch_size=32, shuffle=True)
    val_loader = DataLoader(val_dataset, batch_size=500)

    # Normalization
    y_train = torch.stack([yb for _, yb in train_dataset])
    mean_y = y_train.mean()
    std_y = y_train.std()
    def normalize_targets(y): return (y - mean_y) / std_y
    def denormalize_targets(y): return y * std_y + mean_y

    # -----------------------------
    # 2️⃣ Model, loss, optimizer
    # -----------------------------
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = LSTMModel(input_size=num_features).to(device)
    criterion = nn.SmoothL1Loss()
    optimizer = torch.optim.Adam(model.parameters(), lr=1e-3, weight_decay=1e-4)

    # -----------------------------
    # 3️⃣ Training loop
    # -----------------------------
    num_epochs = 35
    patience = 3
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
                out_seconds = denormalize_targets(out_norm)  # <- correctly denormalize
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

    # -----------------------------
    # 4️⃣ Plot training
    # -----------------------------
    plotInfoTrainingGraph = {
        "type": "line",
        "x": [list(range(1, len(train_losses)+1)), list(range(1, len(val_losses)+1))],
        "y": [[denormalize_targets(loss).item() for loss in train_losses], [val_losses[i] for i in range(len(val_losses))]],
        "xlabel": "Epoch",
        "ylabel": "MAE (seconds)",
        "title": "Training vs Validation Loss",
        "label": ["Train Loss", "Validation Loss"]
    }

    plotInfoGapGraph = {
        "type": "line",
        "x": [list(range(1, len(train_losses)+1))],  # same x for all points
        "y": [[val_losses[i] - train_losses[i] * std_y.item() for i in range(len(train_losses))]],  # scale training loss properly
        "xlabel": "Epoch",
        "ylabel": "Validation - Training Loss (seconds)",
        "title": "Generalization Gap",
        "label": ["Gap"]
    }

    # -----------------------------
    # 5️⃣ Test evaluation
    # -----------------------------
    if best_val_loss < float("inf"):
        checkpoint = torch.load(os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}.pt"))
        model.load_state_dict(checkpoint["state_dict"])

    test_loader = DataLoader(test_dataset, batch_size=128)
    model.eval()
    all_true, all_pred, all_lengths = [], [], []
    missingEdges = 0
    with torch.no_grad():
        for xb, yb in test_loader:
            xb, yb = xb.to(device), yb.to(device)
            out_norm = model(xb)
            out_seconds = denormalize_targets(out_norm)
            all_true.append(yb.cpu())
            all_pred.append(out_seconds.cpu())
            # Compute route lengths (number of non-padding steps per sequence)
            token_mask = (xb.abs().sum(dim=-1) > 0).cpu()   # True where step is not padding
            lengths = token_mask.sum(dim=-1).numpy()       # count non-padding steps
            all_lengths.extend(lengths)
#            batch_lengths_cm = []
#            all_lengths_cm = []
#
#            # Use full precision from your database
#            for route in xb.cpu().numpy():  # shape: [seq_len, num_features]
#                route_length_cm = 0
#                # mask: True where step is not padding
#                non_padding_mask = (np.abs(route).sum(axis=-1) > 0)
#
#                for edge_vector, keep in zip(route, non_padding_mask):
#                    if not keep:
#                        continue  # skip padding
#
#                    edge_tuple = tuple(edge_vector)  # full 17-decimal precision
#                    length = vector_to_length.get(edge_tuple)
#                    if length is not None:
#                        route_length_cm += length
#                    # else: silently skip missing edges
#
#                batch_lengths_cm.append(route_length_cm)
#            all_lengths_cm.extend(batch_lengths_cm)

        all_true = torch.cat(all_true).numpy()
        all_pred = torch.cat(all_pred).numpy()
        all_lengths = np.array(all_lengths)
        test_mae = np.mean(np.abs(all_pred - all_true))
    print(f"Final Test MAE: {test_mae:.4f} seconds")

    num_bins = 10
    bins = np.linspace(all_lengths.min(), all_lengths.max(), num_bins + 1)
    bin_mae = []

    for i in range(num_bins):
        in_bin = (all_lengths >= bins[i]) & (all_lengths < bins[i+1])
        if np.sum(in_bin) == 0:
            bin_mae.append(np.nan)  # or 0 if you prefer
        else:
            mae_bin = np.mean(np.abs(all_pred[in_bin] - all_true[in_bin]))
            bin_mae.append(mae_bin)

    bin_centers = (bins[:-1] + bins[1:]) / 2

# -------------------------
# 1️⃣ Plot: MAE per route length
# -------------------------
    plotInfoMAEPerLength = {
        "type": "bar",
        "x": bin_centers,
        "y": bin_mae,
        "width": (bins[1] - bins[0]) * 0.9,
        "xlabel": "Route Length (number of steps)",
        "ylabel": "MAE (seconds)",
        "title": "MAE per Route Length Bucket"
    }

# -------------------------
# 2️⃣ Plot: Route length histogram
# -------------------------
    plotInfoRouteLengthHist = {
        "type": "bar",  # Use "bar" so it looks like MAE plot
        "x": (bins[:-1] + bins[1:]) / 2,  # bin centers
        "y": np.histogram(all_lengths, bins=bins)[0],  # counts per bin
        "width": (bins[1] - bins[0]) * 0.9,
        "xlabel": "Route Length (number of steps)",
        "ylabel": "Number of Routes",
        "title": "Route Length Distribution (10 Equal Bins)",
        "bin_lines": bins  # custom key for dashed lines
    }

#    num_bins_cm = 10  # for example
#   bins_cm = np.linspace(all_lengths_cm.min(), all_lengths_cm.max(), num_bins_cm + 1)
#    bin_mae_cm = []

#    for i in range(num_bins_cm):
#        in_bin = (all_lengths_cm >= bins_cm[i]) & (all_lengths_cm < bins_cm[i+1])
#        if np.sum(in_bin) == 0:
#            bin_mae_cm.append(np.nan)  # or 0 if you prefer
#        else:
#            mae_bin = np.mean(np.abs(all_pred[in_bin] - all_true[in_bin]))
#            bin_mae_cm.append(mae_bin)

#    bin_centers_cm = (bins_cm[:-1] + bins_cm[1:]) / 2

#    plotInfoMAEPerLengthCM = {
#        "type": "bar",
#        "x": bin_centers_cm,
#        "y": bin_mae_cm,
#        "width": (bins_cm[1] - bins_cm[0]) * 0.9,
#        "xlabel": "Route Length (cm)",
#        "ylabel": "MAE (seconds)",
#        "title": "MAE per Route Length (CM) Bucket",
#        "bin_lines": bins_cm
#    }

    GenerateFigure(
        plot_infos=[plotInfoTrainingGraph, plotInfoMAEPerLength, plotInfoRouteLengthHist, plotInfoGapGraph],
        output_path=os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}_TrainingData.png")
    )

    # -----------------------------
    # 6️⃣ README generation
    # -----------------------------
    feedforward_layers = []
    for layer in model.fc_layers:
        if isinstance(layer, nn.Linear):
            feedforward_layers.append(f"Linear({layer.in_features}->{layer.out_features})")
        elif isinstance(layer, nn.ReLU):
            feedforward_layers.append("ReLU")
        elif isinstance(layer, nn.Dropout):
            feedforward_layers.append(f"Dropout({layer.p})")
    feedforward_layers_str = " → ".join(feedforward_layers)
    dropout_value = [layer.p for layer in model.fc_layers if isinstance(layer, nn.Dropout)][0]

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
| **MAE (Mean Absolute Error)** | {denormalize_targets(torch.tensor(train_losses[-1])):.2f} s | {denormalize_targets(torch.tensor(val_losses[-1])):.2f} s | {test_mae:.2f} s |

---

## 5️⃣ Training Dynamics
- **Loss Progression:** see plot below (train vs. validation loss)

![Training Plot]({ModelName}_TrainingData.png)

---
"""

    with open(readme_path, "w") as f:
        f.write(readme_content)

    print(f"Training done. Results saved to {readme_path}")