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
    
    # Load with weights_only=False to handle the Sequential layers
    checkpoint = torch.load(checkpoint_path, map_location="cpu", weights_only=False)
    
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
    
    # Extract normalization parameters
    normalization = checkpoint.get("normalization", {"mean": 0.0, "std": 1.0})
    
    return model, normalization


def predict_total_time(route, ModelName):
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
    
    mean_y = normalization["mean"]
    std_y = normalization["std"]
    
    with torch.no_grad():
        # Convert route to tensor [1, seq_len, input_size]
        x = torch.tensor(route, dtype=torch.float32).unsqueeze(0)
        
        # Get normalized prediction
        out_norm = model(x)
        
        # Denormalize to get actual seconds
        out_seconds = out_norm * std_y + mean_y
        
        return out_seconds.item()