from pathlib import Path
from torch.nn.utils.rnn import pad_sequence
import torch
from TrainingModel import LSTMModel

# Load checkpoint
model_path = Path(__file__).parent / "Datasets" / "best_model.pt"
checkpoint = torch.load(model_path, map_location="cpu")

# Determine input_size from the checkpoint's weights
# weight_ih_l0 shape is (4 * hidden_size, input_size)
input_size = checkpoint["state_dict"]["lstm.weight_ih_l0"].shape[1]

model = LSTMModel(input_size=input_size)
model.load_state_dict(checkpoint["state_dict"])

normalization = checkpoint["normalization"]
mean_y = normalization["mean"]
std_y = normalization["std"]

def predict_total_time(route, time_bucket=0, day_of_week=0):
    import numpy as np
    
    model.eval()
    with torch.no_grad():
        # Sinusoidal encoding for time bucket (same as training)
        time_sin = np.sin(2 * np.pi * time_bucket / 288)
        time_cos = np.cos(2 * np.pi * time_bucket / 288)
        
        # Append sin/cos time encoding AND day_of_week to each edge vector in the route
        modified_route = [edge + [time_sin, time_cos, day_of_week] for edge in route]
        x = torch.tensor(modified_route, dtype=torch.float32).unsqueeze(0)  # [1, seq_len, input_size]
        
        # Model outputs one scalar
        out_norm = model(x)  # [1, 1]
        
        # De-normalize
        out_seconds = out_norm * std_y + mean_y
        
        return out_seconds.item()