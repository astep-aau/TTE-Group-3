# LSTM Model Training Results

**Date:** 2025-11-27 09:37:54  
**Project:** stringq - Route Time Estimation / Sequence Prediction  

---

## 1️⃣ Dataset Overview
| Property | Value |
|----------|-------|
| **Number of Samples (Train/Val/Test)** | 7 / 1 / 2 |
| **Route Length (Smallest/Largest)** | 1 / 9 |
| **Number of Input Features** | 128 |
| **Output** | 1 (Route Time in Seconds) |

---

## 2️⃣ Model Architecture
| Component | Configuration |
|-----------|---------------|
| **Model Type** | LSTMModel |
| **Input Features** | 128 |
| **Hidden Size** | 64 |
| **Dropout** | 0.2 |
| **Feedforward Layers** | Linear(64->16) → ReLU → Dropout(0.2) → Linear(16->8) → ReLU → Dropout(0.2) → Linear(8->1) |
| **Total Parameters** | 50849 |

---

## 3️⃣ Training Configuration
| Property | Value |
|----------|-------|
| **Total Epochs** | 53 |
| **Early Stopping** | Yes |
| **Batch Size (Train / Val / Test)** | 64 / 128 / 128 |
| **Optimizer** | Adam |
| **Learning Rate** | 0.002 |
| **Loss Function** | SmoothL1Loss |

---

## 4️⃣ Performance Metrics
| Metric | Train | Validation | Test |
|--------|-------|------------|------|
| **MAE (Mean Absolute Error)** | 69.22 s | 40.21 s | 25.68 s |

---

## 5️⃣ Training Dynamics
- **Loss Progression:** see plot below (train vs. validation loss)

![Training Plot](stringq_TrainingData.png)

---
