using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RouteEstimationService.Helper;

public class NearestNodeFinder
{
    private static readonly Lazy<Dictionary<string, NodeData>> NodeCache = new (LoadNodesInternal);
    private const string NodeCoordinatesFile = "Datasets/vertex.csv";

    private class NodeData
    {
        public double Lat { get; init; }
        public double Lon { get; init; }
    }

    public static bool TryGetCoordinates(string nodeId, out double lat, out double lon)
    {
        if (NodeCache.Value.TryGetValue(nodeId, out var node))
        {
            lat = node.Lat;
            lon = node.Lon;
            return true;
        }

        lat = 0;
        lon = 0;
        return false;
    }

    public static string NearestNode(string latStr, string lonStr)
    {
        if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            throw new ArgumentException("Invalid latitude or longitude input.");

        string nearestNodeId = null;
        var minDistance = double.MaxValue;
        const double maxAcceptableDistanceMeters = 1000.0;

        foreach ((string key, var node) in NodeCache.Value)
        {
            double dist = Haversine(lat, lon, node.Lat, node.Lon);
            if (!(dist < minDistance)) continue;
            minDistance = dist;
            nearestNodeId = key;
        }

        if (minDistance > maxAcceptableDistanceMeters)
        {
            throw new Exception($"No nearby node found within {maxAcceptableDistanceMeters} meters.");
        }

        return nearestNodeId ?? throw new Exception("No nodes found in NodeCoordinates.csv.");
    }

    private static Dictionary<string, NodeData> LoadNodesInternal()
    {
        Console.WriteLine(NodeCoordinatesFile);

        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NodeCoordinatesFile);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Node coordinates file not found: '{path}'");

            var cache = new Dictionary<string, NodeData>(StringComparer.Ordinal);

            foreach (string rawLine in File.ReadLines(path))
            {
                if (rawLine == "node_id,longitude,latitude") continue;
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if ("#".StartsWith(line)) continue;

                string[] parts = line.Split(',');
                if (parts.Length < 3) continue;

                string id = parts[0].Trim().Trim('"');
                string lonStr = parts[1].Trim().Trim('"');
                string latStr = parts[2].Trim().Trim('"');

                if (string.IsNullOrEmpty(id)) continue;

                double lat = double.Parse(latStr, CultureInfo.InvariantCulture);
                double lon = double.Parse(lonStr, CultureInfo.InvariantCulture);

                cache[id] = new NodeData { Lat = lat, Lon = lon };
            }

            if (cache.Count == 0)
                throw new Exception("No valid nodes with lat/lon found in file.csv.");

            return cache;
        }
        catch (IOException ex)
        {
            throw new Exception("Failed to load file.csv", ex);
        }
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371e3;
        double phi1 = lat1 * Math.PI / 180;
        double phi2 = lat2 * Math.PI / 180;
        double deltaPhi = (lat2 - lat1) * Math.PI / 180;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return r * c;
    }
}