import matplotlib
matplotlib.use("Agg")  # Use non-interactive backend
import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torch.optim.lr_scheduler import ReduceLROnPlateau
import numpy as np
import os
import random
from pathlib import Path
from .TrainingModel import LSTMModel
import matplotlib.pyplot as plt
import json
import datetime

# Toggle target normalization here. Set to False to disable normalization (training uses raw seconds).
USE_TARGET_NORMALIZATION = False

# Random seed for reproducibility
RANDOM_SEED = 42

# Outlier smoothing configuration (clips extreme values to percentile bounds)
OUTLIER_SMOOTHING_ENABLED = True
OUTLIER_PERCENTILE_CLIP = (5, 95)  # Clip to 5th-95th percentile

def set_seed(seed):
    """Set random seeds for reproducibility."""
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)
    torch.backends.cudnn.deterministic = True
    torch.backends.cudnn.benchmark = False


def compute_clip_bounds(y, lower_pct=1, upper_pct=99):
    """
    Compute percentile bounds from data (should only be called on training data).
    
    Args:
        y: Array/list of total times (training set only)
        lower_pct: Lower percentile bound (e.g., 1 for 1st percentile)
        upper_pct: Upper percentile bound (e.g., 99 for 99th percentile)
    
    Returns:
        Tuple of (lower_bound, upper_bound)
    """
    y_array = np.array(y)
    lower_bound = np.percentile(y_array, lower_pct)
    upper_bound = np.percentile(y_array, upper_pct)
    return lower_bound, upper_bound


def apply_clip_bounds(y, lower_bound, upper_bound):
    """
    Apply pre-computed clipping bounds to data.
    
    Args:
        y: Array/list of total times
        lower_bound: Pre-computed lower bound
        upper_bound: Pre-computed upper bound
    
    Returns:
        Clipped y array and statistics dict
    """
    y_array = np.array(y)
    
    clipped_low = int(np.sum(y_array < lower_bound))
    clipped_high = int(np.sum(y_array > upper_bound))
    
    y_clipped = np.clip(y_array, lower_bound, upper_bound)
    
    stats = {
        "lower_bound": lower_bound,
        "upper_bound": upper_bound,
        "clipped_low": clipped_low,
        "clipped_high": clipped_high,
    }
    
    return y_clipped, stats


def collate_fn_dynamic_padding(batch):
    """
    Custom collate function for dynamic per-batch padding.
    
    Pads sequences only to the max length within the current batch,
    rather than a global max length. This saves memory and computation.
    
    Args:
        batch: List of (sequence, target, length) tuples
    
    Returns:
        Padded sequences tensor, targets tensor, lengths tensor
    """
    sequences, targets, lengths = zip(*batch)
    
    # Find max length in this batch
    batch_max_len = max(lengths)
    num_features = sequences[0].shape[-1]
    
    # Create padded tensor for this batch only
    batch_size = len(sequences)
    padded = torch.zeros(batch_size, batch_max_len, num_features, dtype=torch.float32)
    
    for i, (seq, length) in enumerate(zip(sequences, lengths)):
        padded[i, :length, :] = seq[:length]
    
    targets = torch.stack(targets)
    lengths = torch.tensor(lengths, dtype=torch.long)
    
    return padded, targets, lengths

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
        # Set random seed for reproducibility
        set_seed(RANDOM_SEED)

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

        # -----------------------------
        # Split indices FIRST (before any preprocessing)
        # This prevents data leakage from test/val into training preprocessing
        # -----------------------------
        total_size = len(X)
        indices = list(range(total_size))
        
        # Shuffle indices with fixed seed for reproducibility
        rng = np.random.default_rng(RANDOM_SEED)
        rng.shuffle(indices)
        
        test_size = int(0.2 * total_size)
        train_val_size = total_size - test_size
        val_size = int(0.1 * total_size)
        train_size = train_val_size - val_size
        
        train_indices = indices[:train_size]
        val_indices = indices[train_size:train_size + val_size]
        test_indices = indices[train_size + val_size:]
        
        # Extract data for each split
        X_train = [X[i] for i in train_indices]
        X_val = [X[i] for i in val_indices]
        X_test = [X[i] for i in test_indices]
        y_train = [y[i] for i in train_indices]
        y_val = [y[i] for i in val_indices]
        y_test = [y[i] for i in test_indices]

        # -----------------------------
        # Outlier Smoothing (Percentile Clipping)
        # IMPORTANT: Bounds computed ONLY on training data to prevent data leakage
        # -----------------------------
        outlier_stats = None
        clip_lower_bound, clip_upper_bound = None, None
        
        if OUTLIER_SMOOTHING_ENABLED:
            print(f"\n=== Outlier Smoothing (Percentile Clipping: {OUTLIER_PERCENTILE_CLIP[0]}%-{OUTLIER_PERCENTILE_CLIP[1]}%) ===")
            print(f"Computing bounds from TRAINING data only ({len(y_train)} samples)")
            print(f"Before clipping (train): mean={np.mean(y_train):.2f}s, std={np.std(y_train):.2f}s, min={np.min(y_train):.2f}s, max={np.max(y_train):.2f}s")
            
            # Compute bounds ONLY from training data
            clip_lower_bound, clip_upper_bound = compute_clip_bounds(
                y_train, OUTLIER_PERCENTILE_CLIP[0], OUTLIER_PERCENTILE_CLIP[1]
            )
            print(f"Clipping bounds (from train): [{clip_lower_bound:.2f}s, {clip_upper_bound:.2f}s]")
            
            # Apply bounds to each split
            y_train, train_clip_stats = apply_clip_bounds(y_train, clip_lower_bound, clip_upper_bound)
            y_val, val_clip_stats = apply_clip_bounds(y_val, clip_lower_bound, clip_upper_bound)
            y_test, test_clip_stats = apply_clip_bounds(y_test, clip_lower_bound, clip_upper_bound)
            
            print(f"Train: clipped {train_clip_stats['clipped_low']} low, {train_clip_stats['clipped_high']} high")
            print(f"Val:   clipped {val_clip_stats['clipped_low']} low, {val_clip_stats['clipped_high']} high")
            print(f"Test:  clipped {test_clip_stats['clipped_low']} low, {test_clip_stats['clipped_high']} high")
            print(f"After clipping (train): mean={np.mean(y_train):.2f}s, std={np.std(y_train):.2f}s")
            print("=" * 60 + "\n")
            
            total_clipped_low = train_clip_stats['clipped_low'] + val_clip_stats['clipped_low'] + test_clip_stats['clipped_low']
            total_clipped_high = train_clip_stats['clipped_high'] + val_clip_stats['clipped_high'] + test_clip_stats['clipped_high']
            
            outlier_stats = {
                "total_samples": total_size,
                "clipped_low": total_clipped_low,
                "clipped_high": total_clipped_high,
                "lower_bound": clip_lower_bound,
                "upper_bound": clip_upper_bound,
                "percentile_range": OUTLIER_PERCENTILE_CLIP,
            }

        # -----------------------------
        # Prepare data for dynamic batching (NO global padding)
        # Sequences remain as variable-length lists; padding happens per-batch
        # -----------------------------
        num_features = len(X[0][0])
        max_len = max(len(seq) for seq in X)  # For informational purposes only
        
        def prepare_dataset(X_split, y_split):
            """Convert split data to tensors without padding."""
            sequences = []
            targets = []
            lengths = []
            for seq, target in zip(X_split, y_split):
                seq_tensor = torch.tensor(seq, dtype=torch.float32)
                sequences.append(seq_tensor)
                targets.append(torch.tensor([target], dtype=torch.float32))
                lengths.append(len(seq))
            return list(zip(sequences, targets, lengths))
        
        train_data = prepare_dataset(X_train, y_train)
        val_data = prepare_dataset(X_val, y_val)
        test_data = prepare_dataset(X_test, y_test)

        # Create DataLoaders with dynamic padding collate function
        train_loader = DataLoader(train_data, batch_size=64, shuffle=True, collate_fn=collate_fn_dynamic_padding)
        val_loader = DataLoader(val_data, batch_size=64, collate_fn=collate_fn_dynamic_padding)

        # Normalization
        # We always compute mean/std for informational purposes, but whether they are applied
        # during training/inference is controlled by USE_TARGET_NORMALIZATION.
        y_train_tensor = torch.stack([item[1] for item in train_data])
        mean_y = y_train_tensor.mean()
        std_y = y_train_tensor.std()

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

        print(f"dropout_value: {dropout_value}")
        # Learning rate scheduler - reduces LR when validation loss plateaus
        scheduler = ReduceLROnPlateau(optimizer, mode='min', factor=0.5, patience=5)

        # -----------------------------
        # Training loop
        # -----------------------------
        num_epochs = 100
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
            print(f"Epoch {epoch+1}/{num_epochs}, Train Loss: {epoch_loss:.4f}, Val Loss: {val_loss:.4f}, LR: {optimizer.param_groups[0]['lr']:.6f}")

            # Step the learning rate scheduler based on validation loss
            scheduler.step(val_loss)

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

        test_loader = DataLoader(test_data, batch_size=64, collate_fn=collate_fn_dynamic_padding)
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
            plot_infos=[plotInfoTrainingGraph, plotInfoMAEPerLength, plotInfoRouteLengthHist],
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

        # Build outlier smoothing section for README
        outlier_section = ""
        if outlier_stats:
            outlier_section = f"""
    ## Data Quality / Outlier Smoothing
    | Property | Value |
    |----------|-------|
    | **Method** | Percentile Clipping ({outlier_stats['percentile_range'][0]}%-{outlier_stats['percentile_range'][1]}%) |
    | **Total Samples** | {outlier_stats['total_samples']} (all kept) |
    | **Clipped Low (too fast)** | {outlier_stats['clipped_low']} values smoothed up |
    | **Clipped High (too slow)** | {outlier_stats['clipped_high']} values smoothed down |
    | **TotalTime Bounds** | [{outlier_stats['lower_bound']:.1f}s, {outlier_stats['upper_bound']:.1f}s] |

    ---
"""

        readme_content = f"""# LSTM Model Training Results

    **Date:** {datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}  
    **Project:** {ModelName} - Route Time Estimation / Sequence Prediction  

    ---
{outlier_section}
    ## Dataset Overview
    | Property | Value |
    |----------|-------|
    | **Number of Samples (Train/Val/Test)** | {train_size} / {val_size} / {test_size} |
    | **Route Length (Smallest/Largest)** | {min(len(seq) for seq in X)} / {max(len(seq) for seq in X)} |
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
