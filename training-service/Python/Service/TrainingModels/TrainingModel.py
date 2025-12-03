import torch
import torch.nn as nn

class LSTMModel(nn.Module):
    def __init__(self, input_size, hidden_size=128, dropout=0.1):
        super().__init__()
        # LSTM layer
        self.lstm = nn.LSTM(input_size, hidden_size, batch_first=True)
        
        # Feedforward layers as a sequential block
        self.fc_layers = nn.Sequential(
            nn.Linear(hidden_size, 64),
            nn.ReLU(),
            #nn.Dropout(dropout),
            nn.Linear(64, 32),
            nn.ReLU(),
            #nn.Dropout(dropout),
            nn.Linear(32, 16),
            nn.ReLU(),
            #nn.Dropout(dropout),
            nn.Linear(16, 1),
        )

    def forward(self, x):
        output_seq, _ = self.lstm(x)            # [batch, seq_len, hidden_size]
        last_timestep = output_seq[:, -1, :]  
        out = self.fc_layers(last_timestep)
        return out