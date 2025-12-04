from fastapi import FastAPI, File, UploadFile, HTTPException
from typing import List
from pathlib import Path
import sys
import json
import logging
from contextlib import asynccontextmanager
import shutil

# Setup logging for the python API
logger = logging.getLogger("Python Controller")
logger.setLevel(logging.INFO)
if not logger.handlers:
    ch = logging.StreamHandler()
    ch.setFormatter(logging.Formatter(
        "\033[1m%(name)s\033[0m - "
        "\033[33m%(levelname)s\033[0m - "
        "%(message)s"
    ))
    logger.addHandler(ch)

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Service"
sys.path.append(str(helpers_path))

from TrainingModels.predictTime import predict_total_time
from DatasetCreation.dataCreation import GenerateRoutes
from DatasetCreation.timeCreation import EdgeTraversalTime
from DatasetCreation.getEdgeToVectors import GetEdgeToVectors
from VectorEmbedding.Edge2Vec import VectorEmbedding
from TrainingModels.LSTMTraining import TrainLSTMModel


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan context manager for future startup/shutdown tasks.
    """
    logger.info("🔵 Starting Python API...")
    logger.info("✅ Database connections will be created per-request")
    
    yield
    
    logger.info("🔵 Shutting down API...")


# -----------------------------
# FastAPI App
# -----------------------------
app = FastAPI(title="TTE Python API Controller", lifespan=lifespan)


# -----------------------------
#     Endpoints
# -----------------------------
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




@app.post("/Python/calculate-route-time")
def calculateRouteTime(route: List[int], time_bucket: int = 0):
    return EdgeTraversalTime(route, time_bucket)



@app.post("/Python/vectors")
def getEdgeToVectors(edges: List[int], time_bucket: int = 0):
    return GetEdgeToVectors(edges, time_bucket)



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
    try:
        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
    finally:
        file.file.close()
        
    return {"status": "ok", "file_saved": str(file_path)}