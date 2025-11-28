import torch
import torch.nn as nn

class LSTMModel(nn.Module):
    def __init__(self, input_size, hidden_size=64, dropout=0.2):
        super().__init__()
        self.lstm = nn.LSTM(input_size, hidden_size, batch_first=True)
        self.fc_layers = nn.Sequential(
            nn.Linear(hidden_size, 16),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(16, 8),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(8, 1)
        )

    def forward(self, x):
        output_seq, _ = self.lstm(x)       # output_seq: [batch, seq_len, hidden_size]
        out = output_seq[:, -1, :]         # Take last timestep: [batch, hidden_size]
        out = self.fc_layers(out)
        return out
