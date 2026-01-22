import sys
import os

# Add current directory to path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from TrainingModels.LSTMTraining import TrainLSTMModel

if __name__ == "__main__":
    print("Starting training...")
    TrainLSTMModel("FixedPadding")
    print("Training finished.")
