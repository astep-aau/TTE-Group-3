# LSTM Model Training Results

    **Date:** 2025-12-06 19:48:12  
    **Project:** LSTM 2.0 (Experimental) - Route Time Estimation / Sequence Prediction  

    ---

    ## Data Quality / Outlier Smoothing
    | Property | Value |
    |----------|-------|
    | **Method** | Percentile Clipping (5%-95%) |
    | **Total Samples** | 150000 (all kept) |
    | **Clipped Low (too fast)** | 7524 values smoothed up |
    | **Clipped High (too slow)** | 7484 values smoothed down |
    | **TotalTime Bounds** | [42.8s, 1278.9s] |

    ---

    ## Dataset Overview
    | Property | Value |
    |----------|-------|
    | **Number of Samples (Train/Val/Test)** | 105000 / 15000 / 30000 |
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
    | **Total Epochs** | 77 |
    | **Early Stopping** | Yes |
    | **Batch Size (Train / Val / Test)** | 64 / 64 / 64 |
    | **Optimizer** | Adam |
    | **Learning Rate** | 3.125e-05 |
    | **Loss Function** | SmoothL1Loss |

    ---

    ## Performance Metrics
    | Metric | Train | Validation | Test |
    |--------|-------|------------|------|
    | **MAE (Mean Absolute Error)** | 84.34 s | 74.38 s | 74.93 s |

    ---

    ## Training Dynamics
    - **Loss Progression:** see plot below (train vs. validation loss)

    ![Training Plot](LSTM 2.0 (Experimental)_TrainingData.png)

    ---
    