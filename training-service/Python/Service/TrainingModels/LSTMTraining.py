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

# Toggle target normalization here. Set to False to disable normalization (training uses raw seconds).
USE_TARGET_NORMALIZATION = False

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

        json_path = Path(__file__).parent.parent / "Data" / "TrainingSet.json"
        with open(json_path, "r") as f:
            data = json.load(f)

        X, y = [], []
        for seq in data["Sequences"]:
            edges = seq["Edges"]
            # Guard against zero-length sequences (pack_padded_sequence requires length >= 1)
            if len(edges) == 0:
                print(f"Warning: Skipping sequence with zero edges")
                continue
            X.append(edges)
            y.append(seq.get("TotalTime", 0))

        max_len = max(len(seq) for seq in X)
        num_features = len(X[0][0])
        X_padded = np.zeros((len(X), max_len, num_features), dtype=np.float32)
        lengths = []  # Track actual sequence lengths
        for i, seq in enumerate(X):
            lengths.append(len(seq))
            for j, edge_vector in enumerate(seq):
                X_padded[i, j, :] = edge_vector

        X = torch.tensor(X_padded)
        y = torch.tensor(y, dtype=torch.float32).unsqueeze(1)
        lengths = torch.tensor(lengths, dtype=torch.long)
        dataset = TensorDataset(X, y, lengths)

        # Train/val/test split
        total_size = len(dataset)
        test_size = int(0.2 * total_size)
        train_val_size = total_size - test_size
        val_size = int(0.1 * total_size)
        train_size = train_val_size - val_size
        train_dataset, val_dataset, test_dataset = random_split(dataset, [train_size, val_size, test_size])

        train_loader = DataLoader(train_dataset, batch_size=512, shuffle=True)
        val_loader = DataLoader(val_dataset, batch_size=2048)

        # Normalization
        # We always compute mean/std for informational purposes, but whether they are applied
        # during training/inference is controlled by USE_TARGET_NORMALIZATION.
        y_train = torch.stack([yb for _, yb, _ in train_dataset])
        mean_y = y_train.mean()
        std_y = y_train.std()

        if USE_TARGET_NORMALIZATION:
            def normalize_targets(y): return (y - mean_y) / std_y
            def denormalize_targets(y): return y * std_y + mean_y
            saved_mean = float(mean_y)
            saved_std = float(std_y)
        else:
            # Identity functions when normalization disabled
            def normalize_targets(y): return y
            def denormalize_targets(y): return y
            # Save neutral normalization so inference code stays consistent
            saved_mean = 0.0
            saved_std = 1.0

        # -----------------------------
        # 2️⃣ Model, loss, optimizer
        # -----------------------------
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        model = LSTMModel(input_size=num_features).to(device)
        criterion = nn.SmoothL1Loss()
        optimizer = torch.optim.Adam(model.parameters(), lr=1e-3, weight_decay=1e-4)

        # Extract dropout value from model
        dropout_value = [layer.p for layer in model.fc_layers if isinstance(layer, nn.Dropout)][0]

        # -----------------------------
        # Training loop
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
            for xb, yb, lb in train_loader:
                xb, yb = xb.to(device), yb.to(device)
                # Keep lengths on CPU - pack_padded_sequence requires CPU tensor
                yb_norm = normalize_targets(yb)
                optimizer.zero_grad()
                out = model(xb, lengths=lb)
                loss = criterion(out, yb_norm)
                loss.backward()
                torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)
                optimizer.step()
                epoch_loss += loss.item() * xb.size(0)
            epoch_loss /= len(train_loader.dataset)
            train_losses.append(epoch_loss)

            # Validation
            model.eval()
            val_loss = 0
            with torch.no_grad():
                for xb, yb, lb in val_loader:
                    xb, yb = xb.to(device), yb.to(device)
                    # Keep lengths on CPU - pack_padded_sequence requires CPU tensor
                    yb_norm = normalize_targets(yb)
                    out_norm = model(xb, lengths=lb)
                    val_loss += criterion(out_norm, yb_norm).item() * xb.size(0)
            val_loss /= len(val_loader.dataset)
            val_losses.append(val_loss)  # still normalized
            print(f"Epoch {epoch+1}/{num_epochs}, Train Loss: {epoch_loss:.4f}, Val Loss: {val_loss:.4f}")

            # Early stopping & save best
            if val_loss < best_val_loss:
                best_val_loss = val_loss
                epochs_no_improve = 0
            
                # Extract fc_layers architecture
                fc_architecture = []
                for layer in model.fc_layers:
                    if isinstance(layer, nn.Linear):
                        fc_architecture.append(layer.out_features)
            
                # Create checkpoint dictionary
                checkpoint = {
                    "state_dict": model.state_dict(),
                    # store as plain floats to avoid device/tensor dtype issues at inference
                    # use saved_mean/saved_std so when normalization is disabled we store (0,1)
                    "normalization": {"mean": saved_mean, "std": saved_std, "use_normalization": bool(USE_TARGET_NORMALIZATION)},
                    "input_size": model.lstm.input_size,
                    "hidden_size": model.lstm.hidden_size,
                    "fc_architecture": fc_architecture,
                    "dropout": dropout_value,
                }
            
                os.makedirs(output_dir / f"{ModelName}Model", exist_ok=True)
                torch.save(checkpoint, os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}.pt"))
            else:
                epochs_no_improve += 1
                if epochs_no_improve >= patience:
                    print(f"Early stopping triggered at epoch {epoch+1}")
                    break

        # -----------------------------
        # Plot training
        # -----------------------------
        # Convert recorded losses to seconds for plotting. If we trained on normalized targets
        # we need to scale by std_y; otherwise the losses are already in seconds.
        if USE_TARGET_NORMALIZATION:
            std_val = float(std_y.item()) if isinstance(std_y, torch.Tensor) else float(std_y)
            train_mae_seconds = [loss * std_val for loss in train_losses]
            val_mae_seconds = [loss * std_val for loss in val_losses]
        else:
            train_mae_seconds = list(train_losses)
            val_mae_seconds = list(val_losses)

        plotInfoTrainingGraph = {
            "type": "line",
            "x": [list(range(1, len(train_losses)+1)), list(range(1, len(val_losses)+1))],
            "y": [train_mae_seconds, val_mae_seconds],
            "xlabel": "Epoch",
            "ylabel": "MAE (seconds)",
            "title": "Training vs Validation Loss",
            "label": ["Train Loss", "Validation Loss"]
        }

        plotInfoGapGraph = {
            "type": "line",
            "x": [list(range(1, len(train_losses)+1))],   # wrap in list
            "y": [[((val_losses[i] - train_losses[i]) * (float(std_y.item()) if USE_TARGET_NORMALIZATION else 1.0)) for i in range(len(train_losses))]],  # wrap in list
            "xlabel": "Epoch",
            "ylabel": "Validation - Training Loss (seconds)",
            "title": "Generalization Gap",
            "label": ["Gap"]
        }

        # -----------------------------
        # Test evaluation
        # -----------------------------
        # Load best model for evaluation
        best_checkpoint_path = os.path.join(output_dir / f"{ModelName}Model", f"{ModelName}.pt")
        if os.path.exists(best_checkpoint_path):
            best_checkpoint = torch.load(best_checkpoint_path, map_location=device)
            model.load_state_dict(best_checkpoint["state_dict"])

        test_loader = DataLoader(test_dataset, batch_size=2048)
        model.eval()
        all_true, all_pred, all_lengths = [], [], []
        with torch.no_grad():
            for xb, yb, lb in test_loader:
                xb, yb = xb.to(device), yb.to(device)
                # Keep lengths on CPU - pack_padded_sequence requires CPU tensor
                out_norm = model(xb, lengths=lb)
                out_seconds = denormalize_targets(out_norm)
                all_true.append(yb.cpu())
                all_pred.append(out_seconds.cpu())
                # Use actual lengths from dataset (already on CPU)
                all_lengths.extend(lb.numpy())

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
                bin_mae.append(np.nan)
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
            "type": "bar",
            "x": (bins[:-1] + bins[1:]) / 2,
            "y": np.histogram(all_lengths, bins=bins)[0],
            "width": (bins[1] - bins[0]) * 0.9,
            "xlabel": "Route Length (number of steps)",
            "ylabel": "Number of Routes",
            "title": "Route Length Distribution (10 Equal Bins)",
            "bin_lines": bins
        }

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

        readme_path = output_dir / f"{ModelName}Model" / "README.md"

        readme_content = f"""# LSTM Model Training Results

    **Date:** {datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}  
    **Project:** {ModelName} - Route Time Estimation / Sequence Prediction  

    ---

    ## Dataset Overview
    | Property | Value |
    |----------|-------|
    | **Number of Samples (Train/Val/Test)** | {train_size} / {val_size} / {test_size} |
    | **Route Length (Smallest/Largest)** | {(X.abs().sum(dim=2) != 0).sum(dim=1).min().item()} / {(X.abs().sum(dim=2) != 0).sum(dim=1).max().item()} |
    | **Number of Input Features** | {num_features} |
    | **Output** | 1 (Route Time in Seconds) |

    ---

    ## Model Architecture
    | Component | Configuration |
    |-----------|---------------|
    | **Model Type** | {type(model).__name__} |
    | **Input Features** | {num_features} |
    | **Hidden Size** | {model.lstm.hidden_size} |
    | **Dropout** | {dropout_value} |
    | **Feedforward Layers** | {feedforward_layers_str} |
    | **Total Parameters** | {sum(p.numel() for p in model.parameters())} |

    ---

    ## Training Configuration
    | Property | Value |
    |----------|-------|
    | **Total Epochs** | {epoch + 1} |
    | **Early Stopping** | {"Yes" if epochs_no_improve >= patience else "No"} |
    | **Batch Size (Train / Val / Test)** | {train_loader.batch_size} / {val_loader.batch_size} / {test_loader.batch_size} |
    | **Optimizer** | {type(optimizer).__name__} |
    | **Learning Rate** | {optimizer.param_groups[0]['lr']} |
    | **Loss Function** | {type(criterion).__name__} |

    ---

    ## Performance Metrics
    | Metric | Train | Validation | Test |
    |--------|-------|------------|------|
    | **MAE (Mean Absolute Error)** | {train_mae_seconds[-1]:.2f} s | {val_mae_seconds[-1]:.2f} s | {test_mae:.2f} s |

    ---

    ## Training Dynamics
    - **Loss Progression:** see plot below (train vs. validation loss)

    ![Training Plot]({ModelName}_TrainingData.png)

    ---
    """

        with open(readme_path, "w", encoding="utf-8") as f:
            f.write(readme_content)

        print(f"Training done. Results saved to {readme_path}")