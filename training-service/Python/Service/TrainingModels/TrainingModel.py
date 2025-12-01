import torch
import torch.nn as nn

class LSTMModel(nn.Module):
    def __init__(self, input_size, hidden_size=16, dropout=0.2):
        super().__init__()
        # LSTM layer
        self.lstm = nn.LSTM(input_size, hidden_size, batch_first=True)
        
        # Feedforward layers as a sequential block
        self.fc_layers = nn.Sequential(
            nn.Linear(hidden_size, 8),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(8, 1),
        )

    def forward(self, x):
        # LSTM forward pass
        output_seq, _ = self.lstm(x)       # output_seq: [batch, seq_len, hidden_size]
        out = output_seq[:, -1, :]         # Take last timestep output
        
        # Feedforward pass
        out = self.fc_layers(out)
        return out