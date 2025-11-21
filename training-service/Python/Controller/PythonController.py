# PythonController.py
from http.client import HTTPException
import os
from fastapi import FastAPI, File, UploadFile
from typing import List
from pathlib import Path
import sys

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Service"
sys.path.append(str(helpers_path))

from TrainingModels.predictTime import predict_total_time  # your helper function
from DatasetCreation.dataCreation import GenerateRoutes  # your helper function
from DatasetCreation.timeCreation import EdgeTraversalTime  # your helper function
from DatasetCreation.getEdgeToVectors import GetEdgeToVectors  # your helper function
from VectorEmbedding.Edge2Vec import VectorEmbedding  # your helper function
from TrainingModels.LSTMTraining import TrainLSTMModel  # your helper function

app = FastAPI(title="TTE Python API Controller")

@app.post("/Python/predict-time/{ModelName}")
def PredictTime(edges: List[List[float]], ModelName: str):
    return {"predicted_time": predict_total_time(edges, ModelName)}

@app.get("/Python/generate-routes/{NumberOfSequences}/{MinLengthOfSequence}/{MaxLengthOfSequence}")
def generateRoutes(NumberOfSequences: int, MinLengthOfSequence: int, MaxLengthOfSequence: int):
    # Validate input
    if NumberOfSequences <= 0:
        raise HTTPException(status_code=400, detail="number_of_sequences must be > 0")
    if MinLengthOfSequence <= 0 or MaxLengthOfSequence <= 0:
        raise HTTPException(status_code=400, detail="min_length and max_length must be > 0")
    if MinLengthOfSequence > MaxLengthOfSequence:
        raise HTTPException(status_code=400, detail="min_length cannot be greater than max_length")

    try:
        routes = GenerateRoutes(
            numberOfSequences=NumberOfSequences,
            minLengthOfSequence=MinLengthOfSequence,
            maxLengthOfSequence=MaxLengthOfSequence
        )

        print(routes, file=sys.stderr, flush=True)
        return {"routes": routes}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    
# Endpoint for a single route
@app.post("/Python/calculate-route-time")
def calculateRouteTime(route: List[int]):
    return EdgeTraversalTime(route)

@app.post("/Python/vectors")
def getEdgeToVectors(edges: List[int]):
    return GetEdgeToVectors(edges)

@app.post("/Python/vector-embedding")
def vectorEmbedding():
    VectorEmbedding()
    return {"status": "Edge embeddings generated successfully."}

@app.post("/Python/train-lstm/{ModelName}")
def trainLSTMModel(ModelName: str):
    TrainLSTMModel(ModelName)
    return {"status": "LSTM model trained successfully."}

@app.post("/Python/TrainingFile")
async def upload(file: UploadFile = File(...)):
    file_path = Path(__file__).parent.parent / "Service" / "Data" / "TrainingSet.json"
    with open(file_path, "wb") as f:
        f.write(await file.read())
    return {"status": "ok", "file_saved": file_path}