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
            nn.Dropout(dropout),
            nn.Linear(64, 32),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(32, 16),
            nn.ReLU(),
            nn.Dropout(dropout),
            nn.Linear(16, 1),
        )

    def forward(self, x, lengths):
        # Pack the sequence to ignore padding
        # enforce_sorted=False allows us to pass unsorted batches
        packed_x = nn.utils.rnn.pack_padded_sequence(x, lengths.cpu(), batch_first=True, enforce_sorted=False)
        
        # LSTM returns packed output and (hidden_state, cell_state)
        # hidden_state is (num_layers * num_directions, batch, hidden_size)
        # We only need the last layer's hidden state
        _, (hidden, _) = self.lstm(packed_x)
        
        # Extract the last layer's hidden state
        # hidden[-1] corresponds to the last layer
        last_timestep = hidden[-1]
        
        out = self.fc_layers(last_timestep)
        return out