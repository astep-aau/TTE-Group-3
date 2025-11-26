from fastapi import FastAPI, File, UploadFile, HTTPException
import os
from typing import List
from pathlib import Path
import sys
import json
import sqlite3

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Service"
sys.path.append(str(helpers_path))

from TrainingModels.predictTime import predict_total_time
from DatasetCreation.dataCreation import GenerateRoutes
from DatasetCreation.timeCreation import EdgeTraversalTime
from DatasetCreation.getEdgeToVectors import GetEdgeToVectors
from VectorEmbedding.Edge2Vec import VectorEmbedding
from TrainingModels.LSTMTraining import TrainLSTMModel

app = FastAPI(title="TTE Python API Controller")

def _initialize_resources():
    # -------------------------
    # Load edge embeddings
    # -------------------------
    embedding_path = Path(__file__).parent.parent / "Service" / "Data" / "edgeEmbeddings.json"
    if not embedding_path.is_file():
        raise FileNotFoundError('"edgeEmbeddings.json" does not exist. (Missing Dataset)')

    try:
        with open(embedding_path, "r") as f:
            embedding_cache = json.load(f)
        if not embedding_cache:
            raise ValueError('"edgeEmbeddings.json" is empty.')
    except json.JSONDecodeError as e:
        raise ValueError(f"Invalid JSON in edgeEmbeddings.json: {str(e)}")
    except IOError as e:
        raise RuntimeError(f"Error reading edgeEmbeddings.json: {str(e)}")

    return embedding_cache

# Initialize resources at startup and store in app.state
@app.on_event("startup")
def startup_event():
    try:
        embedding_cache = _initialize_resources()
        app.state.embedding_cache = embedding_cache
        print("Resources initialized successfully at startup (memory-efficient mode)", file=sys.stderr)
    except Exception as e:
        # keep startup but log the error so endpoints can still attempt to initialize lazily
        print(f"Resource initialization failed on startup: {e}", file=sys.stderr)
        app.state.embedding_cache = None

@app.on_event("shutdown")
def shutdown_event():
    """Close database connection on shutdown."""
    if hasattr(app.state, "db_connection") and app.state.db_connection:
        app.state.db_connection.close()
        print("Database connection closed", file=sys.stderr)

@app.post("/Python/predict-time/{ModelName}")
def PredictTime(edges: List[List[float]], ModelName: str):
    return {"predicted_time": predict_total_time(edges, ModelName)}

@app.get("/Python/generate-routes/{NumberOfSequences}/{MinLengthOfSequence}/{MaxLengthOfSequence}")
def generateRoutes(NumberOfSequences: int, MinLengthOfSequence: int, MaxLengthOfSequence: int):
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
        return {"routes": routes}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Endpoint for a single route - pass db connection and embedding cache from app.state
@app.post("/Python/calculate-route-time")
def calculateRouteTime(route: List[int]):
    embedding_cache = getattr(app.state, "embedding_cache", None)
    return EdgeTraversalTime(route, embedding_cache=embedding_cache)

@app.post("/Python/vectors")
def getEdgeToVectors(edges: List[int]):
    embedding_cache = getattr(app.state, "embedding_cache", None)
    return GetEdgeToVectors(edges, embedding_cache=embedding_cache)

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