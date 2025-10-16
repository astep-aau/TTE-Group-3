# training_service/train_route_time_model.py
import sys
import os
import numpy as np

project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.append(project_root)
import matplotlib.pyplot as plt
from models.routeEstimationModel import RouteTimeLSTM
from visualizeTraining import plotGraph
from dataLoader import create_dataloader
import torch
from torch.utils.data import DataLoader, TensorDataset

dataloader = create_dataloader()

# Initialize model
model = RouteTimeLSTM(input_size=3)

# Training loop (simple example)
criterion = torch.nn.MSELoss()
optimizer = torch.optim.Adam(model.parameters(), lr=0.01)
epoch_losses = []
batch_losses = []

for epoch in range(10):
    epoch_loss = 0
    for batch_idx, (batch_X, batch_y) in enumerate(dataloader):
        optimizer.zero_grad()
        outputs = model(batch_X)
        loss = criterion(outputs, batch_y)
        loss.backward()
        optimizer.step()

        batch_losses.append(loss.item())   # track each batch
        epoch_loss += loss.item()

        #print(f"Epoch {epoch+1}, Batch {batch_idx+1}, Loss: {loss.item():.4f}")

    avg_loss = epoch_loss / len(dataloader)
    epoch_losses.append(avg_loss)
    #print(f"--- Epoch {epoch+1} Average Loss: {avg_loss:.4f} ---")

# Save trained model
torch.save(model.state_dict(), "route_time_model.pth")
#print("Training complete")

# --- Visualization ---
plt.figure(figsize=(10,6))
plt.plot(batch_losses, color='red', alpha=0.5, label='Batch Loss (spikes)')
epoch_positions = [len(dataloader) * (i + 1) for i in range(len(epoch_losses))]
plt.plot(epoch_positions, epoch_losses, color='blue', marker='o', label='Epoch Avg Loss')
plt.xlabel('Batch Updates')
plt.ylabel('Loss')
plt.title('Batch vs Epoch Loss Progression')
plt.legend()
plt.grid(True)
plt.show()