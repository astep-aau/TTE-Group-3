using Microsoft.Extensions.Logging;
using Moq;
using translator_service.Domain.Entities;
using translator_service.Features.GetRoute;
using Xunit;

namespace TestTranslatorService;

public class GetRouteHandlerTest
{
    private readonly Mock<IRouteRepository> _repositoryMock;
    private readonly Mock<ILogger<GetRouteHandler>> _loggerMock;
    private readonly GetRouteHandler _handler;

    public GetRouteHandlerTest()
    {
        _repositoryMock = new Mock<IRouteRepository>();
        _loggerMock = new Mock<ILogger<GetRouteHandler>>();
        _handler = new GetRouteHandler(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ReturnsRoute_WhenRepositoryReturnsRoute()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var command = new GetRouteCommand(correlationId);
        var expectedRoute = new RouteResult
        {
            Id = 42,
            CorrelationId = correlationId,
            Origin = "Origin",
            Destination = "Destination",
            DistanceKm = 12.5,
            TravelTimeMinutes = 20.2,
            Path = new List<RouteCoordinate>
            {
                new()
                {
                    Latitude = 1.23,
                    Longitude = 4.56
                }
            }
        };

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRoute);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedRoute, result);
        _repositoryMock.Verify(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNull_WhenRepositoryReturnsNull()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var command = new GetRouteCommand(correlationId);

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RouteResult?)null);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _repositoryMock.Verify(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RethrowsOperationCancelled()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var command = new GetRouteCommand(correlationId);

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_RethrowsUnexpectedException()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var command = new GetRouteCommand(correlationId);

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task SaveRouteAsync_CallsRepository()
    {
        // Arrange
        var route = new RouteResult
        {
            Id = 7,
            CorrelationId = Guid.NewGuid(),
            Origin = "Origin",
            Destination = "Destination",
            DistanceKm = 9.1,
            TravelTimeMinutes = 15.4
        };

        // Act
        await _handler.SaveRouteAsync(route, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.SaveRouteAsync(route, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveRouteAsync_RethrowsOperationCancelled()
    {
        // Arrange
        var route = new RouteResult
        {
            Id = 7,
            CorrelationId = Guid.NewGuid()
        };

        _repositoryMock
            .Setup(r => r.SaveRouteAsync(route, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.SaveRouteAsync(route, CancellationToken.None));
    }

    [Fact]
    public async Task SaveRouteAsync_RethrowsUnexpectedException()
    {
        // Arrange
        var route = new RouteResult
        {
            Id = 7,
            CorrelationId = Guid.NewGuid()
        };

        _repositoryMock
            .Setup(r => r.SaveRouteAsync(route, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.SaveRouteAsync(route, CancellationToken.None));
    }
}

