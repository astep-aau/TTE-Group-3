# LSTM Model Training Results

    **Date:** 2025-12-03 22:41:19  
    **Project:** test100000 - Route Time Estimation / Sequence Prediction  

    ---

    ## Dataset Overview
    | Property | Value |
    |----------|-------|
    | **Number of Samples (Train/Val/Test)** | 70000 / 10000 / 20000 |
    | **Route Length (Smallest/Largest)** | 1 / 100 |
    | **Number of Input Features** | 34 |
    | **Output** | 1 (Route Time in Seconds) |

    ---

    ## Model Architecture
    | Component | Configuration |
    |-----------|---------------|
    | **Model Type** | LSTMModel |
    | **Input Features** | 34 |
    | **Hidden Size** | 128 |
    | **Dropout** | 0.1 |
    | **Feedforward Layers** | Linear(128->64) → ReLU → Dropout(0.1) → Linear(64->32) → ReLU → Dropout(0.1) → Linear(32->16) → ReLU → Dropout(0.1) → Linear(16->1) |
    | **Total Parameters** | 94849 |

    ---

    ## Training Configuration
    | Property | Value |
    |----------|-------|
    | **Total Epochs** | 28 |
    | **Early Stopping** | Yes |
    | **Batch Size (Train / Val / Test)** | 512 / 2048 / 2048 |
    | **Optimizer** | Adam |
    | **Learning Rate** | 0.001 |
    | **Loss Function** | SmoothL1Loss |

    ---

    ## Performance Metrics
    | Metric | Train | Validation | Test |
    |--------|-------|------------|------|
    | **MAE (Mean Absolute Error)** | 116.72 s | 94.89 s | 95.31 s |

    ---

    ## Training Dynamics
    - **Loss Progression:** see plot below (train vs. validation loss)

    ![Training Plot](test100000_TrainingData.png)

    ---
    