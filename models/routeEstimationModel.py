# ml_models/route_time_lstm.py
import torch
import torch.nn as nn

class RouteTimeLSTM(nn.Module):
    def __init__(self, input_size, hidden_size=64, num_layers=2):
        super(RouteTimeLSTM, self).__init__()
        self.lstm = nn.LSTM(
            input_size=input_size,
            hidden_size=hidden_size,
            num_layers=num_layers,
            batch_first=True
        )
        self.fc = nn.Linear(hidden_size, 1)

    def forward(self, x):
        out, _ = self.lstm(x)
        out = out[:, -1, :]  # last timestep
        out = self.fc(out)
        return out