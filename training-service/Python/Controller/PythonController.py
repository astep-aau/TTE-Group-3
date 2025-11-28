# PythonController.py
from http.client import HTTPException
from fastapi import FastAPI, Body, Query
from typing import List, Optional
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
from DatasetCreation.embeddings_cache import get_embeddings  # embeddings cache

app = FastAPI(title="TTE Python API Controller")

@app.on_event("startup")
async def startup_event():
    """Pre-load embeddings cache at startup to avoid first-request delay"""
    try:
        get_embeddings()
        print("Embeddings cache loaded successfully", file=sys.stderr, flush=True)
    except Exception as e:
        print(f"Warning: Failed to load embeddings cache at startup: {e}", file=sys.stderr, flush=True)

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
def calculateRouteTime(route: List[int], timeBucket: int = None):
    return EdgeTraversalTime(route, timeBucket)

@app.post("/Python/vectors")
def get_edge_to_vectors(
    edges: List[int] = Body(..., example=[1, 2, 3]),
    timeBucket: Optional[int] = Query(
        None, ge=0, le=287,
        description="Optional time bucket (0..287)"
    )
):
    # timeBucket is already validated by Query(ge=0, le=287)
    return GetEdgeToVectors(edges, timeBucket)

@app.post("/Python/vector-embedding")
def vectorEmbedding():
    VectorEmbedding()
    return {"status": "Edge embeddings generated successfully."}

@app.post("/Python/train-lstm/{ModelName}")
def trainLSTMModel(ModelName: str):
    TrainLSTMModel(ModelName)
    return {"status": "LSTM model trained successfully."}