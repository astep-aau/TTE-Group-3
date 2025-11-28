from pathlib import Path
from torch.nn.utils.rnn import pad_sequence
import torch
from .TrainingModel import LSTMModel

def predict_total_time(route, ModelName):
    # Load checkpoint
    model_path = Path(__file__).parent.parent / "Data" / "TrainedModels" / f"{ModelName}.pt"
    checkpoint = torch.load(model_path, map_location="cpu")

    # Dynamically determine input size from the input route
    # route is [seq_len, num_features]
    if not route or len(route) == 0 or len(route[0]) == 0:
        raise ValueError('Route must contain at least one edge with features')
    input_dim = len(route[0])

    # Check for input_size in checkpoint and validate
    checkpoint_input_size = checkpoint.get("input_size")

    if checkpoint_input_size is not None and checkpoint_input_size != input_dim:
        raise ValueError(f"Input size mismatch: checkpoint expects {checkpoint_input_size}, but got {input_dim} from route")
    model = LSTMModel(input_size=input_dim)
    model.load_state_dict(checkpoint["state_dict"])

    normalization = checkpoint["normalization"]
    mean_y = normalization["mean"]
    std_y = normalization["std"]

    model.eval()
    with torch.no_grad():
        x = torch.tensor(route, dtype=torch.float32).unsqueeze(0)  # [1, seq_len, input_dim]
        
        # Model outputs one scalar
        out_norm = model(x)  # [1, 1]
        
        # De-normalize
        out_seconds = out_norm * std_y + mean_y
        
        return out_seconds.item()
