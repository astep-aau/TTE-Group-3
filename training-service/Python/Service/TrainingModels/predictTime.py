from pathlib import Path
from torch.nn.utils.rnn import pad_sequence
import torch
from .TrainingModel import LSTMModel

def predict_total_time(route, ModelName):
    # Load checkpoint
    model_path = Path(__file__).parent.parent / "Data" / "TrainedModels" / f"{ModelName}.pt"
    checkpoint = torch.load(model_path, map_location="cpu")

    # Infer input_size from the route data (each edge is a vector)
    input_size = len(route[0]) if route else 64
    
    model = LSTMModel(input_size=input_size)
    model.load_state_dict(checkpoint["state_dict"])

    normalization = checkpoint["normalization"]
    mean_y = normalization["mean"]
    std_y = normalization["std"]

    model.eval()
    with torch.no_grad():
        x = torch.tensor(route, dtype=torch.float32).unsqueeze(0)  # [1, seq_len, input_size]
        
        # Model outputs one scalar
        out_norm = model(x)  # [1, 1]
        
        # De-normalize
        out_seconds = out_norm * std_y + mean_y
        
        return out_seconds.item()
