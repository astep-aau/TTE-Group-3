using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RouteEstimationService.Helper;

public class NearestNodeFinder
{
    private static Dictionary<string, NodeData> _nodeCache;
    private static bool _isLoaded;
    private const string RoadNetworkFile = "Datasets/RoadNetwork.json";

    private class NodeData
    {
        [JsonPropertyName("lat")]
        public double Lat { get; init; }
        [JsonPropertyName("lon")]
        public double Lon { get; init; }
    }

    public static string NearestNode(string latStr, string lonStr)
    {
        if (!_isLoaded)
            LoadNodes();

        if (!double.TryParse(latStr, out double lat) || !double.TryParse(lonStr, out double lon))
            throw new ArgumentException("Invalid latitude or longitude input.");

        string nearestNodeId = null;
        var minDistance = double.MaxValue;

        foreach ((string key, var node) in _nodeCache)
        {
            double dist = Haversine(lat, lon, node.Lat, node.Lon);
            if (!(dist < minDistance)) continue;
            minDistance = dist;
            nearestNodeId = key;
        }

        return nearestNodeId ?? throw new Exception("No nodes found in RoadNetwork.json.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private static void LoadNodes()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RoadNetworkFile);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Road network file not found: {path}");

            string json = File.ReadAllText(path);
            var allNodes = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json, JsonOptions);
            if (allNodes == null || allNodes.Count == 0)
                throw new Exception("RoadNetwork.json is empty or invalid.");

            _nodeCache = new Dictionary<string, NodeData>(allNodes.Count);
            foreach (var kvp in allNodes)
            {
                if (!kvp.Value.TryGetValue("lat", out object latObj) ||
                    !kvp.Value.TryGetValue("lon", out object lonObj)) continue;
                if (latObj is JsonElement latElem && lonObj is JsonElement lonElem &&
                    latElem.TryGetDouble(out double latVal) && lonElem.TryGetDouble(out double lonVal))
                {
                    _nodeCache[kvp.Key] = new NodeData { Lat = latVal, Lon = lonVal };
                }
                else if (double.TryParse(latObj?.ToString(), out double latVal2) && double.TryParse(lonObj?.ToString(), out double lonVal2))
                {
                    _nodeCache[kvp.Key] = new NodeData { Lat = latVal2, Lon = lonVal2 };
                }
            }
            if (_nodeCache.Count == 0)
                throw new Exception("No valid nodes with lat/lon found in RoadNetwork.json.");
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to load RoadNetwork.json", ex);
        }
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371e3; // metres
        double phi1 = lat1 * Math.PI / 180;
        double phi2 = lat2 * Math.PI / 180;
        double deltaPhi = (lat2 - lat1) * Math.PI / 180;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        double d = r * c;
        return d;
    }
}