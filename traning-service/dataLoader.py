import torch
from torch.utils.data import Dataset, DataLoader

# Custom Dataset for variable-length sequences (ragged sequences)
class RaggedDataset(Dataset):
    def __init__(self):
        """
        Initialize the dataset.
        Each element in self.data is a tuple: (sequence_tensor, label_tensor)
        Sequences can have different lengths (different number of timesteps).
        """
        self.data = [
            (torch.tensor([[1.0, 2.0, 3.0],   # 2 timesteps, 3 features
                           [4.0, 5.0, 6.0]]), 
             torch.tensor([1.0])),            # Label for this sequence
             
            (torch.tensor([[2.0, 3.0, 4.0],   # 3 timesteps, 3 features
                           [5.0, 6.0, 7.0],
                           [8.0, 9.0, 10.0]]), 
             torch.tensor([2.0]))             # Label for this sequence
        ]

    def __len__(self):
        """
        Return the number of sequences in the dataset.
        """
        return len(self.data)

    def __getitem__(self, idx):
        """
        Return the sequence and label at index `idx`.
        """
        return self.data[idx]

# Function to create a DataLoader from the RaggedDataset
def create_dataloader():
    """
    Returns a PyTorch DataLoader for the ragged dataset.
    - batch_size=1 ensures each sequence is its own batch (required for variable-length sequences)
    - shuffle=True shuffles the sequences each epoch
    """
    dataset = RaggedDataset()
    dataloader = DataLoader(dataset, batch_size=1, shuffle=True)
    return dataloader