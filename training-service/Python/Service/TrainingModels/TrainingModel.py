import torch
import torch.nn as nn
from torch.nn.utils.rnn import pack_padded_sequence, pad_packed_sequence

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

    def forward(self, x, lengths=None):
        """
        Args:
            x: Padded input tensor [batch, max_seq_len, input_size]
            lengths: Actual sequence lengths for each sample [batch].
                     Must be a CPU tensor (pack_padded_sequence requirement).
                     If None, assumes no padding (uses last timestep).
        """
        if lengths is not None:
            # Pack the padded sequence to ignore padding in LSTM computation
            # lengths should already be on CPU; ensure it defensively
            lengths_cpu = lengths.cpu() if lengths.is_cuda else lengths
            packed = pack_padded_sequence(x, lengths_cpu, batch_first=True, enforce_sorted=False)
            packed_out, (h_n, c_n) = self.lstm(packed)
            # h_n[-1] contains the hidden state at the ACTUAL last timestep for each sequence
            last_timestep = h_n[-1]  # [batch, hidden_size]
        else:
            # Fallback for inference without lengths (single unpadded sequence)
            output_seq, _ = self.lstm(x)
            last_timestep = output_seq[:, -1, :]
        
        out = self.fc_layers(last_timestep)
        return out