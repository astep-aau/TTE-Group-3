import json
import numpy as np
from tensorflow.keras.models import Sequential
from tensorflow.keras.layers import LSTM, Dense
from sklearn.model_selection import train_test_split
import matplotlib.pyplot as plt
import os

# 1️⃣ Load JSON data
json_path = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets/TrainingSet.JSON"
with open(json_path, "r") as f:
    data = json.load(f)

# 2️⃣ Extract edge sequences and target times
X = []
y = []

for seq in data["Sequences"]:
    edges = seq["Edges"]  # list of vectors
    total_time = seq.get("TotalTime", 0)  # numeric target
    X.append(edges)
    y.append(total_time)

# Determine max sequence length and feature size
max_len = max(len(seq) for seq in X)
num_features = len(X[0][0])

# Manually pad sequences with zeros
X_padded = np.zeros((len(X), max_len, num_features), dtype=np.float32)
for i, seq in enumerate(X):
    for j, edge_vector in enumerate(seq):
        X_padded[i, j, :] = edge_vector

X = X_padded
y = np.array(y, dtype=np.float32)

# 3️⃣ Train/test split
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

# 4️⃣ Build LSTM model
sequence_length = X.shape[1]
num_features = X.shape[2]

model = Sequential()
model.add(LSTM(128, input_shape=(sequence_length, num_features), return_sequences=False))
model.add(Dense(4, activation="relu"))
model.add(Dense(1))  # output: predicted total time

model.compile(optimizer="adam", loss="mse", metrics=["mse"])

# 5️⃣ Train
history = model.fit(
    X_train, y_train,
    validation_data=(X_test, y_test),
    epochs=50,
    batch_size=16
)

# 6️⃣ Evaluate
loss, mse = model.evaluate(X_test, y_test)
print(f"Test Loss: {loss:.4f}, Test MSE: {mse:.4f}")

# 7️⃣ Predict example
predictions = model.predict(X_test[:5])
print("Predictions:", predictions.flatten())
print("Actual:", y_test[:5])

# 8️⃣ Plot MSE evolution and save safely
output_dir = "/Users/emilskov/RiderProjects/P5 - Time Travel Estimation/training-service/Helpers/Datasets"
os.makedirs(output_dir, exist_ok=True)

plot_path = os.path.join(output_dir, "training_loss.png")
plt.plot(history.history['loss'], label='Train Loss')
plt.plot(history.history['val_loss'], label='Validation Loss')
plt.xlabel('Epochs')
plt.ylabel('MSE')
plt.title('Training Loss over Time')
plt.legend()
plt.savefig(plot_path)
plt.close()

print(f"Training loss plot saved to: {plot_path}")
