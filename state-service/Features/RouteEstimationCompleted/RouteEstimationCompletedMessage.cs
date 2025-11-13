using System;
using System.Collections.Generic;

namespace StateService.Features.RouteEstimationCompleted
{
    public record RouteEstimationCompletedMessage(
        Guid CorrelationId,
        string Origin,
        string Destination,
        double DistanceKm,
        double TravelTimeMinutes,
        IReadOnlyList<RouteCoordinate> Path);

    public record RouteCoordinate(double Latitude, double Longitude);
}

