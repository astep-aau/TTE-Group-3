# LSTM Model Training Results

**Date:** 2025-11-21 11:42:00  
**Project:** 100FINALTEST4 - Route Time Estimation / Sequence Prediction  

---

## 1️⃣ Dataset Overview
| Property | Value |
|----------|-------|
| **Number of Samples (Train/Val/Test)** | 140 / 20 / 40 |
| **Route Length (Smallest/Largest)** | 2 / 100 |
| **Number of Input Features** | 16 |
| **Output** | 1 (Route Time in Seconds) |

---

## 2️⃣ Model Architecture
| Component | Configuration |
|-----------|---------------|
| **Model Type** | LSTMModel |
| **Input Features** | 16 |
| **Hidden Size** | 64 |
| **Dropout** | 0.2 |
| **Feedforward Layers** | Linear(64->16) → ReLU → Dropout(0.2) → Linear(16->8) → ReLU → Dropout(0.2) → Linear(8->1) |
| **Total Parameters** | 22177 |

---

## 3️⃣ Training Configuration
| Property | Value |
|----------|-------|
| **Total Epochs** | 24 |
| **Early Stopping** | Yes |
| **Batch Size (Train / Val / Test)** | 20 / 100 / 100 |
| **Optimizer** | Adam |
| **Learning Rate** | 0.001 |
| **Loss Function** | L1Loss |

---

## 4️⃣ Performance Metrics
| Metric | Train | Validation | Test |
|--------|-------|------------|------|
| **MAE (Mean Absolute Error)** | 687.77 s | 197.48 s | 163.80 s |

---

## 5️⃣ Training Dynamics
- **Loss Progression:** see plot below (train vs. validation loss)

![Training Plot](100FINALTEST4_TrainingData.png)

---
