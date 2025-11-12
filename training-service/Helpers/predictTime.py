from pathlib import Path
from torch.nn.utils.rnn import pad_sequence
import torch
from LSTMTraining import LSTMModel

# Load checkpoint
model_path = Path(__file__).parent / "Datasets" / "best_model.pt"
checkpoint = torch.load(model_path, map_location="cpu")

model = LSTMModel(input_size=5)
model.load_state_dict(checkpoint["state_dict"])

normalization = checkpoint["normalization"]
mean_y = normalization["mean"]
std_y = normalization["std"]
example_edges = [
        [2.9264252185821533, 6.7298188805580139, 3.8162198066711426, 40.6825737953186035, 0.5089805126190186],
         [2.9264252185821533, -6.7298188805580139, 3.8162198066711426, 40.6825737953186035, -0.5089805126190186],
         [2.9264252185821533, 6.7298188805580139, 3.8162198066711426, -40.6825737953186035, 0.5089805126190186],
    ]

def predict_time(sequence, model, normalization):
    x = torch.tensor(sequence, dtype=torch.float32).unsqueeze(0)

    model.eval()
    with torch.no_grad():
        output_norm = model(x)                  
        output_seconds = output_norm * normalization["std"] + normalization["mean"]
        output_seconds = torch.relu(output_seconds)
        return output_seconds.item()   
                
predicted_times = predict_time(example_edges, model, normalization  )
print("Predicted times (seconds):", predicted_times)