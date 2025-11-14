using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RouteEstimationService.Helper;

// For finding the nearest node present in our road network given user inputted coordinates.

public class NearestNodeFinder
{
    private static Dictionary<string, NodeData> _nodeCache;
    private static bool _isLoaded;
    private const string NodeCoordinatesFile = "Datasets/vertex.csv";

    private class NodeData
    {
        public double Lat { get; init; }
        public double Lon { get; init; }
    }
    
    // Add inside class NearestNodeFinder in `Helper/NearestNodeFinder.cs`
    public static bool TryGetCoordinates(string nodeId, out double lat, out double lon)
    {
        if (!_isLoaded)
            LoadNodes();
    
        if (_nodeCache != null && _nodeCache.TryGetValue(nodeId, out var node))
        {
            lat = node.Lat;
            lon = node.Lon;
            return true;
        }
    
        lat = default;
        lon = default;
        return false;
    }
    

    public static string NearestNode(string latStr, string lonStr)
    {
        if (!_isLoaded)
            LoadNodes();

        if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            throw new ArgumentException("Invalid latitude or longitude input.");

        string nearestNodeId = null;
        var minDistance = double.MaxValue;
        const double maxAcceptableDistanceMeters = 1000.0;


        foreach (var (key, node) in _nodeCache)
        {
            double dist = Haversine(lat, lon, node.Lat, node.Lon);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestNodeId = key;
            }
        }
        
        if (minDistance > maxAcceptableDistanceMeters)
        {
            throw new Exception($"No nearby node found within {maxAcceptableDistanceMeters} meters.");
        }

        return nearestNodeId ?? throw new Exception("No nodes found in NodeCoordinates.csv.");
    }

    private static void LoadNodes()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, NodeCoordinatesFile);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Node coordinates file not found: '{path}'");

            _nodeCache = new Dictionary<string, NodeData>(StringComparer.Ordinal);

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#")) continue; 

                var parts = line.Split(',');
                if (parts.Length < 3) continue;

                var id = parts[0].Trim().Trim('"');
                var lonStr = parts[1].Trim().Trim('"'); //File format is ID, Lon, Lat
                var latStr = parts[2].Trim().Trim('"');

                if (string.IsNullOrEmpty(id)) continue;

                // Optional header handling: skip if non-numeric coordinates
                if (!double.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;

                _nodeCache[id] = new NodeData { Lat = lat, Lon = lon };
            }

            if (_nodeCache.Count == 0)
                throw new Exception("No valid nodes with lat/lon found in NodeCoordinates.csv.");

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load NodeCoordinates.csv", ex);
        }
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371e3; // Earth's radius in meters
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

