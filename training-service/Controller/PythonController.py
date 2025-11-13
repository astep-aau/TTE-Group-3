# PythonController.py
from fastapi import FastAPI
from pydantic import BaseModel
from pathlib import Path
import sys

# Add Helpers folder to sys.path
helpers_path = Path(__file__).parent.parent / "Helpers"
sys.path.append(str(helpers_path))

from predictTime import predict_total_time  # your helper function

app = FastAPI(title="TTE Python API Controller")

# Define the input model
class Route(BaseModel):
    edges: list[list[float]]  # a list of edge vectors

@app.post("/Python/predict-time")
def predict_time_endpoint(route: Route):
    return {"predicted_time": predict_total_time(route.edges)}
