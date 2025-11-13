using System;
using System.Collections.Generic;

// For converting a list of Node IDs to their corresponding coordinates.

namespace RouteEstimationService.Helper;

public static class NodeIDToCoordinates
{
    public sealed class Coordinate
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public static List<Coordinate> Map(IReadOnlyList<string> nodeIds)
    {
        if (nodeIds is null) throw new ArgumentNullException(nameof(nodeIds));

        var result = new List<Coordinate>(nodeIds.Count);
        foreach (var id in nodeIds)
        {
            if (!NearestNodeFinder.TryGetCoordinates(id, out var lat, out var lon))
                throw new KeyNotFoundException($"Node ID '{id}' not found in cache.");

            result.Add(new Coordinate { Lat = lat, Lon = lon });
        }
        return result;
    }
}
