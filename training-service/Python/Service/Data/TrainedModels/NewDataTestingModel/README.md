# LSTM Model Training Results

**Date:** 2025-11-28 17:34:05  
**Project:** NewDataTesting - Route Time Estimation / Sequence Prediction  

---

## 1️⃣ Dataset Overview
| Property | Value |
|----------|-------|
| **Number of Samples (Train/Val/Test)** | 1398 / 199 / 399 |
| **Route Length (Smallest/Largest)** | 1 / 55 |
| **Number of Input Features** | 32 |
| **Output** | 1 (Route Time in Seconds) |

---

## 2️⃣ Model Architecture
| Component | Configuration |
|-----------|---------------|
| **Model Type** | LSTMModel |
| **Input Features** | 32 |
| **Hidden Size** | 16 |
| **Dropout** | 0.2 |
| **Feedforward Layers** | Linear(16->8) → ReLU → Dropout(0.2) → Linear(8->1) |
| **Total Parameters** | 3345 |

---

## 3️⃣ Training Configuration
| Property | Value |
|----------|-------|
| **Total Epochs** | 14 |
| **Early Stopping** | Yes |
| **Batch Size (Train / Val / Test)** | 32 / 500 / 128 |
| **Optimizer** | Adam |
| **Learning Rate** | 0.001 |
| **Loss Function** | SmoothL1Loss |

---

## 4️⃣ Performance Metrics
| Metric | Train | Validation | Test |
|--------|-------|------------|------|
| **MAE (Mean Absolute Error)** | 326.35 s | 14478.77 s | 74.52 s |

---

## 5️⃣ Training Dynamics
- **Loss Progression:** see plot below (train vs. validation loss)

![Training Plot](NewDataTesting_TrainingData.png)

---
