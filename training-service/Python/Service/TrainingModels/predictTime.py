from pathlib import Path
import torch
import torch.nn as nn


class LSTMModelFromCheckpoint(nn.Module):
    """
    Reconstructs the exact LSTM + feedforward model from a checkpoint.
    Supports multiple architectures: 16->8->1 and 16->4->1
    """
    def __init__(self, input_size, hidden_size, fc_architecture, dropout=0.2):
        super().__init__()
        # LSTM layer
        self.lstm = nn.LSTM(
            input_size=input_size,
            hidden_size=hidden_size,
            batch_first=True
        )
        
        # Build fc_layers based on architecture
        # fc_architecture should be like [8, 1] or [4, 1]
        layers = []
        in_features = hidden_size
        
        for i, out_features in enumerate(fc_architecture):
            layers.append(nn.Linear(in_features, out_features))
            
            # Add ReLU and Dropout after all but the last layer
            if i < len(fc_architecture) - 1:
                layers.append(nn.ReLU())
                layers.append(nn.Dropout(dropout))
            
            in_features = out_features
        
        self.fc_layers = nn.Sequential(*layers)

    def forward(self, x):
        output_seq, _ = self.lstm(x)
        out = output_seq[:, -1, :]  # Take last timestep output
        return self.fc_layers(out)


def load_model_from_checkpoint(checkpoint_path):
    """
    Loads the model and normalization dictionary from a checkpoint file.
    Supports both new format (with fc_architecture) and old format (auto-detect).
    """
    checkpoint_path = Path(checkpoint_path)
    if not checkpoint_path.exists():
        raise FileNotFoundError(f"Checkpoint not found: {checkpoint_path}")
    
    # Load checkpoint onto CPU
    checkpoint = torch.load(checkpoint_path, map_location="cpu")
    
    # Extract architecture parameters
    input_size = checkpoint["input_size"]
    hidden_size = checkpoint["hidden_size"]
    dropout = checkpoint.get("dropout", 0.2)
    
    # Try to get fc_architecture from checkpoint (new format)
    if "fc_architecture" in checkpoint:
        fc_architecture = checkpoint["fc_architecture"]
    else:
        # Fallback: detect from state_dict (for old checkpoints)
        state_dict = checkpoint["state_dict"]
        fc_architecture = []
        
        i = 0
        while f"fc_layers.{i}.weight" in state_dict:
            layer_key = f"fc_layers.{i}.weight"
            out_features = state_dict[layer_key].shape[0]
            fc_architecture.append(out_features)
            
            if out_features == 1:  # Last layer
                break
            i += 3  # Skip Linear, ReLU, Dropout
    
    # Reconstruct model with detected architecture
    model = LSTMModelFromCheckpoint(input_size, hidden_size, fc_architecture, dropout)
    
    # Load the saved weights
    model.load_state_dict(checkpoint["state_dict"])
    model.eval()
    
    # Extract normalization parameters and coerce to python primitives
    norm = checkpoint.get("normalization", None)
    if isinstance(norm, dict):
        mean = float(norm.get("mean", 0.0))
        std = float(norm.get("std", 1.0))
        use_norm = bool(norm.get("use_normalization", True))
    else:
        # Backwards compatibility: no normalization dict present
        mean = 0.0
        std = 1.0
        use_norm = False

    normalization = {"mean": mean, "std": std, "use_normalization": use_norm}

    return model, normalization


def predict_total_time(route, ModelName, ignore_checkpoint_normalization: bool = False):
    """
    Predicts total time for a given route using a saved checkpoint.
    
    Args:
        route: list of edge vectors (list of lists or 2D array)
        ModelName: string - name of the model
        
    Returns:
        float: predicted total time in seconds
    """
    checkpoint_path = Path(__file__).parent.parent / "Data" / "TrainedModels" / f"{ModelName}Model" / f"{ModelName}.pt"
    model, normalization = load_model_from_checkpoint(checkpoint_path)
    
    # Decide which normalization to use. If caller requests to ignore checkpoint
    # normalization (ignore_checkpoint_normalization=True) we use neutral values
    # mean=0.0 and std=1.0 so the model output is returned as raw seconds.
    if ignore_checkpoint_normalization:
        mean_y = 0.0
        std_y = 1.0
    else:
        mean_y = float(normalization.get("mean", 0.0))
        std_y = float(normalization.get("std", 1.0))
    use_norm_flag = bool(normalization.get("use_normalization", True))

    # Informational print so users know which normalization was applied
    print(f"predict_total_time: using normalization mean={mean_y}, std={std_y}, "
          f"ignored_flag={ignore_checkpoint_normalization}, checkpoint_use_norm={use_norm_flag}")
    
    with torch.no_grad():
        # Convert route to tensor [1, seq_len, input_size]
        x = torch.tensor(route, dtype=torch.float32).unsqueeze(0)
        
    # Get normalized prediction tensor
    out_norm = model(x)

    # Denormalize (this will be a no-op when std=1, mean=0)
    out_seconds = out_norm * std_y + mean_y

    # Return scalar Python float
    return float(out_seconds.squeeze().item())