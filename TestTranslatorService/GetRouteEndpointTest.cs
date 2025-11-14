using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using translator_service.Domain.Entities;
using translator_service.Domain.Events;
using translator_service.Endpoints;
using translator_service.Features.GetRoute;
using Xunit;

namespace TestTranslatorService;

public class GetRouteEndpointTest
{
    private readonly Mock<IRouteRepository> _repositoryMock;
    private readonly Mock<ILogger<GetRouteHandler>> _handlerLoggerMock;
    private readonly GetRouteHandler _handler;
    private readonly Mock<ILogger<GetRouteEndpoint>> _endpointLoggerMock;
    private readonly Mock<IBus> _busMock;
    private readonly RouteDeliveredEmitter _emitter;
    private readonly Mock<ILogger<RouteDeliveredEmitter>> _emitterLoggerMock;
    private readonly GetRouteEndpoint _endpoint;

    public GetRouteEndpointTest()
    {
        _repositoryMock = new Mock<IRouteRepository>();
        _handlerLoggerMock = new Mock<ILogger<GetRouteHandler>>();
        _endpointLoggerMock = new Mock<ILogger<GetRouteEndpoint>>();
        _busMock = new Mock<IBus>();
        _busMock
            .Setup(bus => bus.Publish(It.IsAny<RouteDeliveredEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _emitterLoggerMock = new Mock<ILogger<RouteDeliveredEmitter>>();
        _emitter = new RouteDeliveredEmitter(_busMock.Object, _emitterLoggerMock.Object);

        _handler = new GetRouteHandler(_repositoryMock.Object, _handlerLoggerMock.Object);
        _endpoint = new GetRouteEndpoint(_handler, _emitter, _endpointLoggerMock.Object);
    }

    [Fact]
    public async Task GetRouteAsync_ReturnsOk_WhenRouteExists()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var route = new RouteResult
        {
            CorrelationId = correlationId,
            Origin = "Origin",
            Destination = "Destination",
            DistanceKm = 21.2,
            TravelTimeMinutes = 33.3,
            Path = new List<RouteCoordinate>
            {
                new()
                {
                    Latitude = 55.5,
                    Longitude = 12.3
                }
            }
        };

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

        // Act
        var result = await _endpoint.GetRouteAsync(correlationId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RouteResultDto>(okResult.Value);
        Assert.Equal(route.CorrelationId, dto.CorrelationId);
        Assert.Equal(route.Origin, dto.Origin);
        Assert.Equal(route.Destination, dto.Destination);
        Assert.Equal(route.DistanceKm, dto.DistanceKm);
        Assert.Equal(route.TravelTimeMinutes, dto.TravelTimeMinutes);
        Assert.Single(dto.Path);
        Assert.Equal(route.Path[0].Latitude, dto.Path[0].Latitude);
        Assert.Equal(route.Path[0].Longitude, dto.Path[0].Longitude);

        _repositoryMock.Verify(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(bus => bus.Publish(
            It.Is<RouteDeliveredEvent>(evt => evt.CorrelationId == correlationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRouteAsync_ReturnsNotFound_WhenRouteMissing()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RouteResult?)null);

        // Act
        var result = await _endpoint.GetRouteAsync(correlationId, CancellationToken.None);

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        _repositoryMock.Verify(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(bus => bus.Publish(It.IsAny<RouteDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRouteAsync_ReturnsBadRequest_WhenCorrelationIdMissing()
    {
        // Arrange
        var correlationId = Guid.Empty;

        // Act
        var result = await _endpoint.GetRouteAsync(correlationId, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        _repositoryMock.Verify(r => r.GetRouteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _busMock.Verify(bus => bus.Publish(It.IsAny<RouteDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRouteAsync_Returns499_WhenOperationCancelled()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _endpoint.GetRouteAsync(correlationId, CancellationToken.None);

        // Assert
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(499, status.StatusCode);
        _busMock.Verify(bus => bus.Publish(It.IsAny<RouteDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRouteAsync_Returns500_WhenUnhandledException()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetRouteAsync(correlationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        // Act
        var result = await _endpoint.GetRouteAsync(correlationId, CancellationToken.None);

        // Assert
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
        _busMock.Verify(bus => bus.Publish(It.IsAny<RouteDeliveredEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

