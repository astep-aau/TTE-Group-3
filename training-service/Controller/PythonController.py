# PythonController.py
from http.client import HTTPException
from fastapi import FastAPI
from typing import List
from pathlib import Path
from pydantic import BaseModel
import sys

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Helpers"
sys.path.append(str(helpers_path))

from predictTime import predict_total_time  # your helper function
from dataCreation import GenerateRoutes  # your helper function
from timeCreation import EdgeTraversalTime  # your helper function
from getEdgeToVectors import GetEdgeToVectors  # your helper function
from Edge2Vec import VectorEmbedding  # your helper function
from LSTMTraining import TrainLSTMModel  # your helper function

app = FastAPI(title="TTE Python API Controller")

# Request model for route time calculation
class RouteTimeRequest(BaseModel):
    route: List[int]
    day_number: int = 0

# Request model for LSTM prediction
class PredictionRequest(BaseModel):
    edges: List[List[float]]  # Route with embeddings already attached
    time_bucket: int = 0       # 0-287 (5-minute intervals in a day)
    day_of_week: int = 0       # 0-6 (Monday=0)

@app.post("/Python/predict-time")
def PredictTime(request: PredictionRequest):
    return {
        "predicted_time": predict_total_time(
            request.edges, 
            request.time_bucket, 
            request.day_of_week
        )
    }

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
def calculateRouteTime(request: RouteTimeRequest):
    return EdgeTraversalTime(request.route, request.day_number)

@app.post("/Python/vectors")
def getEdgeToVectors(edges: List[int]):
    return GetEdgeToVectors(edges)

@app.post("/Python/vector-embedding")
def vectorEmbedding():
    VectorEmbedding()
    return {"status": "Edge embeddings generated successfully."}

@app.post("/Python/train-lstm")
def trainLSTMModel():
    TrainLSTMModel()
    return {"status": "LSTM model trained successfully."}