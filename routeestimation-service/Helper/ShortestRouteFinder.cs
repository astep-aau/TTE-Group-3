using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Globalization;
using FluentResults;
using RouteEstimationService.Domain.Entities;

namespace RouteEstimationService.Helper;

public class ShortestRouteFinder
{
    private static Dictionary<string, NodeData> _nodeCache;
    private static Dictionary<int, EdgeData> _edgeCache;
    private static bool _isLoaded;
    private const string RoadNetworkFile = "Datasets/RoadNetwork.json";
    private const string EdgeFile = "Datasets/edge_traversals_processed.json";

    private class NodeData
    {
        public List<int> OutwardEdges { get; init; }
        public List<string> OutwardVertices { get; init; }
        public List<int> BackwardEdges { get; init; }
        public List<string> BackwardVertices { get; init; }
        public double Lat { get; init; }
        public double Lon { get; init; }
    }

    private class EdgeData
    {
        public double LengthCm { get; init; }
        public bool Oneway { get; init; }
    }

    public static Result<RouteResult> ShortestRoute(string originNodeId, string destinationNodeId)
    {
        if (!_isLoaded)
            LoadData();
        if (!_nodeCache.TryGetValue(originNodeId, out NodeData originNode) || !_nodeCache.TryGetValue(destinationNodeId, out NodeData destinationNode))
            return Result.Ok(new RouteResult());    // Return empty route if nodes not found

        var openSet = new PriorityQueue<string, double>();
        var cameFrom = new Dictionary<string, (string prevNode, int edgeId)>();
        var gScore = new Dictionary<string, double>();
        var fScore = new Dictionary<string, double>();

        foreach (string nodeId in _nodeCache.Keys)
        {
            gScore[nodeId] = double.MaxValue;
            fScore[nodeId] = double.MaxValue;
        }
        gScore[originNodeId] = 0;
        fScore[originNodeId] = Haversine(originNode, destinationNode);
        openSet.Enqueue(originNodeId, fScore[originNodeId]);

        while (openSet.Count > 0)
        {
            string current = openSet.Dequeue();
            if (current == destinationNodeId)
                return Result.Ok(ReconstructPath(cameFrom, current));

            var node = _nodeCache[current];
            // Outward traversal (always allowed)
            for (int i = 0; i < node.OutwardEdges.Count; i++)
            {
                int edgeId = node.OutwardEdges[i];
                string neighbor = node.OutwardVertices[i];
                if (!_nodeCache.ContainsKey(neighbor) || !_edgeCache.TryGetValue(edgeId, out var edge)) continue;
                double tentativeGScore = gScore[current] + edge.LengthCm;
                if (!(tentativeGScore < gScore[neighbor])) continue;
                cameFrom[neighbor] = (current, edgeId);
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + Haversine(_nodeCache[neighbor], destinationNode);
                if (openSet.UnorderedItems.All(x => x.Element != neighbor))
                    openSet.Enqueue(neighbor, fScore[neighbor]);
            }
            // Backward traversal (only if edge is not oneway)
            for (int i = 0; i < node.BackwardEdges.Count; i++)
            {
                int edgeId = node.BackwardEdges[i];
                string neighbor = node.BackwardVertices[i];
                if (!_nodeCache.ContainsKey(neighbor) || !_edge_cache_try(edgeId, out var edge)) continue;
                // Only allow backward traversal if edge is not oneway
                if (edge.Oneway) continue;
                double tentativeGScore = gScore[current] + edge.LengthCm;
                if (!(tentativeGScore < gScore[neighbor])) continue;
                cameFrom[neighbor] = (current, edgeId);
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = tentativeGScore + Haversine(_nodeCache[neighbor], destinationNode);
                if (openSet.UnorderedItems.All(x => x.Element != neighbor))
                    openSet.Enqueue(neighbor, fScore[neighbor]);
            }
        }
        // No route found
        return Result.Ok(new RouteResult());
    }

    // helper to avoid repeated TryGetValue pattern with correct edge lookup
    private static bool _edge_cache_try(int edgeId, out EdgeData edge)
    {
        return _edgeCache.TryGetValue(edgeId, out edge);
    }

    private static RouteResult ReconstructPath(Dictionary<string, (string prevNode, int edgeId)> cameFrom, string current)
    {
        var nodes = new List<string>();
        var edges = new List<int>();
        nodes.Add(current);
        while (cameFrom.ContainsKey(current))
        {
            (string prev, int edgeId) = cameFrom[current];
            edges.Add(edgeId);
            current = prev;
            nodes.Add(current);
        }
        nodes.Reverse();
        edges.Reverse();
        return new RouteResult { NodeIds = nodes, EdgeIds = edges };
    }

    private static void LoadData()
    {
        // Load nodes
        string nodePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RoadNetworkFile);
        string nodeJson = File.ReadAllText(nodePath);
        var nodeDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(nodeJson);
        _nodeCache = new Dictionary<string, NodeData>(nodeDict.Count);
        foreach ((string key, var n) in nodeDict)
        {
            n.TryGetValue("outward_edges", out object valueEdge);
            n.TryGetValue("outward_vertices", out object valueVertex);
            n.TryGetValue("backward_edges", out object valueBackEdge);
            n.TryGetValue("backward_vertices", out object valueBackVertex);
            n.TryGetValue("lat", out object valueLat);
            n.TryGetValue("lon", out object valueLon);
            var nodeData = new NodeData
            {
                OutwardEdges = valueEdge is not null ? JsonArrayToIntList(valueEdge) : new List<int>(),
                OutwardVertices = valueVertex is not null ? JsonArrayToStringList(valueVertex) : new List<string>(),
                BackwardEdges = valueBackEdge is not null ? JsonArrayToIntList(valueBackEdge) : new List<int>(),
                BackwardVertices = valueBackVertex is not null ? JsonArrayToStringList(valueBackVertex) : new List<string>(),
                Lat = ToDouble(valueLat),
                Lon = ToDouble(valueLon)
            };
            _nodeCache[key] = nodeData;
        }
        // Load edges
        string edgePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EdgeFile);
        string edgeJson = File.ReadAllText(edgePath);
        var edgeDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(edgeJson);
        _edgeCache = new Dictionary<int, EdgeData>(edgeDict.Count);
        foreach ((string key, var edge) in edgeDict)
        {
            edge.TryGetValue("length (cm)", out object lengthObj);
            edge.TryGetValue("oneway", out object onewayObj);
            var edgeData = new EdgeData
            {
                LengthCm = ToDouble(lengthObj),
                Oneway = ToBool(onewayObj)
            };
            _edgeCache[int.Parse(key)] = edgeData;
        }
        _isLoaded = true;
    }

    private static List<int> JsonArrayToIntList(object arr)
    {
        var list = new List<int>();
        switch (arr)
        {
            case JsonElement { ValueKind: JsonValueKind.Array } elem:
            {
                foreach (var v in elem.EnumerateArray())
                    if (v.TryGetInt32(out int val)) list.Add(val);
                break;
            }
            case IEnumerable<object> objArr:
            {
                foreach (object v in objArr)
                    if (int.TryParse(v.ToString(), out int val)) list.Add(val);
                break;
            }
        }
        return list;
    }
    private static List<string> JsonArrayToStringList(object arr)
    {
        var list = new List<string>();
        switch (arr)
        {
            case JsonElement { ValueKind: JsonValueKind.Array } elem:
            {
                list.AddRange(elem.EnumerateArray().Select(v => v.ToString()));
                break;
            }
            case IEnumerable<object> objArr:
            {
                list.AddRange(objArr.Select(v => v.ToString()));
                break;
            }
        }
        return list;
    }

    private static double ToDouble(object value)
    {
        switch (value)
        {
            case null:
                return 0;
            case double d:
                return d;
            case float f:
                return f;
            case long l:
                return l;
            case int i:
                return i;
            case JsonElement { ValueKind: JsonValueKind.Number } je when je.TryGetDouble(out double jd):
                return jd;
            case JsonElement je:
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    string s = je.GetString();
                    if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
                }
                if (je.ValueKind == JsonValueKind.True) return 1;
                if (je.ValueKind == JsonValueKind.False) return 0;
                break;
            }
        }

        // fallback: attempt parse from string representation
        var str = value.ToString();
        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedStr)) return parsedStr;
        return 0;
    }

    private static bool ToBool(object value)
    {
        switch (value)
        {
            case null:
                return false;
            case bool b:
                return b;
            case JsonElement { ValueKind: JsonValueKind.True }:
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                return false;
            case JsonElement { ValueKind: JsonValueKind.Number } je when je.TryGetInt32(out int iv):
                return iv != 0;
            case JsonElement je:
            {
                if (je.ValueKind == JsonValueKind.String)
                {
                    string s = je.GetString();
                    if (bool.TryParse(s, out bool parsedBool)) return parsedBool;
                    if (int.TryParse(s, out int parsedInt)) return parsedInt != 0;
                }

                break;
            }
        }

        var str = value.ToString();
        if (bool.TryParse(str, out bool parsed)) return parsed;
        if (int.TryParse(str, out int parsedInt2)) return parsedInt2 != 0;
        return false;
    }

    private static double Haversine(NodeData a, NodeData b)
    {
        const double r = 6371e3; // metres
        double phi1 = a.Lat * Math.PI / 180;
        double phi2 = b.Lat * Math.PI / 180;
        double deltaPhi = (b.Lat - a.Lat) * Math.PI / 180;
        double deltaLambda = (b.Lon - a.Lon) * Math.PI / 180;
        double x = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(1 - x));
        double d = r * c;
        return d;
    }
}