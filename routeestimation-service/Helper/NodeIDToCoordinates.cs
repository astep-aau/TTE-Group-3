using System;
using System.Collections.Generic;

// For converting a list of Node IDs to their corresponding coordinates.

namespace RouteEstimationService.Helper;

public static class NodeIdToCoordinates
{
    public sealed class Coordinate
    {
        public double Lat { get; init; }
        public double Lon { get; init; }
    }

    public static List<Coordinate> Map(IReadOnlyList<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);

        var result = new List<Coordinate>(nodeIds.Count);
        foreach (string id in nodeIds)
        {
            if (!NearestNodeFinder.TryGetCoordinates(id, out double lat, out double lon))
                throw new KeyNotFoundException($"Node ID '{id}' not found in cache.");

            result.Add(new Coordinate { Lat = lat, Lon = lon });
        }
        return result;
    }
}
