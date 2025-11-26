FROM python:3.11-slim

WORKDIR /app

# Install system dependencies for torch and other packages
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for better caching
COPY training-service/Python/requirements.txt .

# Install CPU-only PyTorch first (much smaller than GPU version)
RUN pip install --no-cache-dir torch --index-url https://download.pytorch.org/whl/cpu

# Install remaining dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY training-service/Python/ .

# Expose the FastAPI port
EXPOSE 6969

# Run the FastAPI application
CMD ["uvicorn", "Controller.PythonController:app", "--host", "0.0.0.0", "--port", "6969"]

