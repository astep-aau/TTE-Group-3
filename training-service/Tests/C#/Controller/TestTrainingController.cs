using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.Extensions.Logging;
using TrainingService.Controllers;
using TrainingService.Domain;
using TrainingService.Infrastructure;
using Xunit;

namespace TrainingService.Tests.C_.Controller;

public class TestTrainingController
{
    private readonly Mock<ITrainingQueue> _mockQueue;
    private readonly TrainingController _controller;

    public TestTrainingController()
    {
        _mockQueue = new Mock<ITrainingQueue>();
        _controller = new TrainingController(_mockQueue.Object, new Mock<ILogger<TrainingController>>().Object);
    }

    #region GetStatus Tests

    [Fact]
    public void GetStatus_ReturnsOkResult()
    {
        // Arrange
        StatusTracker.Status = "Test Status";

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetStatus_ReturnsCurrentStatus()
    {
        // Arrange
        const string expectedStatus = "Training in progress";
        StatusTracker.Status = expectedStatus;

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        object? value = okResult.Value;
        var statusProperty = value?.GetType().GetProperty("Status");
        var actualStatus = statusProperty?.GetValue(value) as string;
        Assert.Equal(expectedStatus, actualStatus);
    }

    #endregion

    #region StartTraining - Valid Request Tests

    [Fact]
    public void StartTraining_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void StartTraining_WithValidRequest_EnqueuesCorrectRequest()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        TrainingRequest? enqueuedRequest = null;
        _mockQueue.Setup(q => q.Enqueue(It.IsAny<TrainingRequest>()))
            .Callback<TrainingRequest>(r => enqueuedRequest = r);

        // Act
        _controller.StartTraining(request);

        // Assert
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Once);
        Assert.NotNull(enqueuedRequest);
        Assert.Equal(request.ModelName, enqueuedRequest.ModelName);
        Assert.Equal(request.NumberOfRoutes, enqueuedRequest.NumberOfRoutes);
        Assert.Equal(request.MinLength, enqueuedRequest.MinLength);
        Assert.Equal(request.MaxLength, enqueuedRequest.MaxLength);
    }

    [Fact]
    public void StartTraining_WithValidRequest_ReturnsQueuedStatus()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        object? value = okResult.Value;
        var statusProperty = value?.GetType().GetProperty("Status");
        var status = statusProperty?.GetValue(value) as string;
        Assert.Equal("Queued", status);
    }

    #endregion

    #region StartTraining - Invalid Request Tests (Validation)

    [Fact]
    public void StartTraining_WithEmptyModelName_ReturnsBadRequest()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };
        _controller.ModelState.AddModelError("ModelName", "Required");

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Never);
    }

    [Fact]
    public void StartTraining_WithMissingModelName_ReturnsBadRequest()
    {
        // Arrange
        var request = new TrainingRequest
        {
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };
        _controller.ModelState.AddModelError("ModelName", "Required");

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void StartTraining_WithInvalidNumberOfRoutes_ReturnsBadRequest(int numberOfRoutes)
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = numberOfRoutes,
            MinLength = 5,
            MaxLength = 20
        };
        _controller.ModelState.AddModelError("NumberOfRoutes", "Must be greater than 0");

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartTraining_WithInvalidMinLength_ReturnsBadRequest(int minLength)
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = minLength,
            MaxLength = 20
        };
        _controller.ModelState.AddModelError("MinLength", "Must be greater than 0");

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartTraining_WithInvalidMaxLength_ReturnsBadRequest(int maxLength)
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = maxLength
        };
        _controller.ModelState.AddModelError("MaxLength", "Must be greater than 0");

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.IsAny<TrainingRequest>()), Times.Never);
    }

    #endregion

    #region StartTraining - Error Handling Tests

    [Fact]
    public void StartTraining_WhenEnqueueThrowsException_Returns500()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        _mockQueue.Setup(q => q.Enqueue(It.IsAny<TrainingRequest>()))
            .Throws(new InvalidOperationException("Queue is full"));

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public void StartTraining_WhenEnqueueThrowsException_ReturnsFailedStatus()
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        _mockQueue.Setup(q => q.Enqueue(It.IsAny<TrainingRequest>()))
            .Throws(new Exception("Unexpected error"));

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        object? value = statusResult.Value;
        var statusProperty = value?.GetType().GetProperty("Status");
        var status = statusProperty?.GetValue(value) as string;
        Assert.Equal("Failed", status);
    }

    #endregion

    #region StartTraining - Boundary Tests

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(100, 1, 100)]
    [InlineData(1000, 10, 50)]
    public void StartTraining_WithBoundaryValues_EnqueuesSuccessfully(int numberOfRoutes, int minLength, int maxLength)
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = "LSTM",
            NumberOfRoutes = numberOfRoutes,
            MinLength = minLength,
            MaxLength = maxLength
        };

        // Act
        var result = _controller.StartTraining(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _mockQueue.Verify(q => q.Enqueue(It.Is<TrainingRequest>(r =>
            r.NumberOfRoutes == numberOfRoutes &&
            r.MinLength == minLength &&
            r.MaxLength == maxLength
        )), Times.Once);
    }

    [Theory]
    [InlineData("LSTM")]
    [InlineData("GRU")]
    [InlineData("Transformer")]
    [InlineData("A")]
    [InlineData("Very_Long_Model_Name_With_Special_Characters_123")]
    public void StartTraining_WithVariousModelNames_EnqueuesCorrectly(string modelName)
    {
        // Arrange
        var request = new TrainingRequest
        {
            ModelName = modelName,
            NumberOfRoutes = 1000,
            MinLength = 5,
            MaxLength = 20
        };

        // Act
        _controller.StartTraining(request);

        // Assert
        _mockQueue.Verify(q => q.Enqueue(It.Is<TrainingRequest>(r => r.ModelName == modelName)), Times.Once);
    }

    #endregion
}