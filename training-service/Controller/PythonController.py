# PythonController.py
from fastapi import FastAPI
from typing import List
from pathlib import Path
import sys

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Helpers"
sys.path.append(str(helpers_path))

from predictTime import predict_total_time  # your helper function

app = FastAPI(title="TTE Python API Controller")

@app.post("/Python/predict-time")
def predict_time_endpoint(edges: List[List[float]]):
    return {"predicted_time": predict_total_time(edges)}
