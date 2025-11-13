// csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using OsmSharp;
using OsmSharp.Streams;

namespace OSMNodeExtractor;

static class OSMNodeExtractor
{
    private static readonly string[] NodeIdPropertyNames = new[]
    {
        "id",
        "nodeId",
        "node_id",
        "nodeID"
    };

    private static readonly string[] NodeCollectionPropertyNames = new[]
    {
        "nodes",
        "nodeIds",
        "node_ids",
        "node_refs",
        "nodeRefs"
    };

    public static int Extract(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: OSMNodeExtractor <path-to.osm.pbf> <road-network.json> <output.csv>");
            return 1;
        }

        var pbfPath = Path.GetFullPath(args[0]);
        var roadNetworkPath = Path.GetFullPath(args[1]);
        var outputCsvPath = Path.GetFullPath(args[2]);

        if (!File.Exists(pbfPath))
        {
            Console.Error.WriteLine($"PBF file not found: {pbfPath}");
            return 1;
        }

        if (!File.Exists(roadNetworkPath))
        {
            Console.Error.WriteLine($"JSON file not found: {roadNetworkPath}");
            return 1;
        }

        try
        {
            Console.WriteLine("Parsing road network JSON...");
            using var jsonStream = File.OpenRead(roadNetworkPath);
            using var document = JsonDocument.Parse(jsonStream);

            var nodeIds = ExtractNodeIds(document.RootElement);

            if (nodeIds.Count == 0)
            {
                Console.Error.WriteLine("No node IDs were discovered in the supplied JSON file.");
                return 2;
            }

            Console.WriteLine($"Collected {nodeIds.Count} unique node ID(s) from the JSON file.");

            Console.WriteLine("Scanning OSM PBF for matching nodes (this might take a while)...");
            var foundNodes = FindNodes(pbfPath, nodeIds);

            WriteCsv(outputCsvPath, foundNodes);
            Console.WriteLine($"Wrote {foundNodes.Count} node(s) to {outputCsvPath}.");

            var missing = nodeIds.Except(foundNodes.Keys).ToList();
            if (missing.Count > 0)
            {
                Console.WriteLine($"Warning: {missing.Count} node ID(s) were not located in the PBF file.");
                var missingListPath = Path.ChangeExtension(outputCsvPath, ".missing.txt");
                File.WriteAllLines(missingListPath, missing.Select(id => id.ToString(CultureInfo.InvariantCulture)));
                Console.WriteLine($"Missing IDs have been written to {missingListPath}.");
                return 3;
            }

            Console.WriteLine("All node IDs were successfully resolved.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }

    public static HashSet<long> ExtractNodeIds(JsonElement root)
    {
        var ids = new HashSet<long>();
        ExtractNodeIdsRecursive(root, ids, null);
        return ids;
    }

    private static void ExtractNodeIdsRecursive(JsonElement element, ISet<long> ids, string? propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (TryExtractNodeIdFromPropertyName(property.Name, out var fromName))
                    {
                        ids.Add(fromName);
                    }

                    if (IsNodeIdProperty(property.Name) && TryReadNodeId(property.Value, out var fromProperty))
                    {
                        ids.Add(fromProperty);
                    }

                    ExtractNodeIdsRecursive(property.Value, ids, property.Name);
                }

                break;
            case JsonValueKind.Array:
                var treatAsNodeCollection = propertyName != null && IsNodeCollectionProperty(propertyName);
                foreach (var item in element.EnumerateArray())
                {
                    if (treatAsNodeCollection && TryReadNodeId(item, out var fromArray))
                    {
                        ids.Add(fromArray);
                    }
                    else
                    {
                        ExtractNodeIdsRecursive(item, ids, null);
                    }
                }

                break;
            case JsonValueKind.String:
            case JsonValueKind.Number:
                if (propertyName != null && IsNodeIdProperty(propertyName) && TryReadNodeId(element, out var fromValue))
                {
                    ids.Add(fromValue);
                }

                break;
        }
    }

    private static bool TryReadNodeId(JsonElement element, out long id)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out id))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (!string.IsNullOrWhiteSpace(raw) && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                return true;
            }
        }

        id = default;
        return false;
    }

    private static bool TryExtractNodeIdFromPropertyName(string propertyName, out long id) =>
        long.TryParse(propertyName, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);

    private static bool IsNodeIdProperty(string propertyName) =>
        NodeIdPropertyNames.Any(p => string.Equals(p, propertyName, StringComparison.OrdinalIgnoreCase));

    private static bool IsNodeCollectionProperty(string propertyName) =>
        NodeCollectionPropertyNames.Any(p => string.Equals(p, propertyName, StringComparison.OrdinalIgnoreCase)) ||
        propertyName.Contains("node", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("vertex", StringComparison.OrdinalIgnoreCase);

    public static Dictionary<long, (double Latitude, double Longitude)> FindNodes(string pbfPath, IReadOnlyCollection<long> targetNodeIds)
    {
        var remaining = new HashSet<long>(targetNodeIds);
        var found = new Dictionary<long, (double Latitude, double Longitude)>();

        using var fileStream = File.OpenRead(pbfPath);
        var source = new PBFOsmStreamSource(fileStream);

        foreach (var osmGeo in source)
        {
            if (osmGeo is not Node node || node.Id == null)
            {
                continue;
            }

            var nodeId = node.Id.Value;
            if (!remaining.Contains(nodeId))
            {
                continue;
            }

            if (node.Latitude == null || node.Longitude == null)
            {
                Console.WriteLine($"Node {nodeId} is missing coordinate information in the PBF file.");
                remaining.Remove(nodeId);
                continue;
            }

            found[nodeId] = (node.Latitude.Value, node.Longitude.Value);
            remaining.Remove(nodeId);

            if (remaining.Count == 0)
            {
                break;
            }
        }

        return found;
    }

    public static void WriteCsv(string outputCsvPath, IReadOnlyDictionary<long, (double Latitude, double Longitude)> nodes)
    {
        using var stream = File.Create(outputCsvPath);
        using var writer = new StreamWriter(stream);

        writer.WriteLine("node_id,latitude,longitude");
        foreach (var (nodeId, coordinates) in nodes.OrderBy(pair => pair.Key))
        {
            writer.WriteLine(
                $"{nodeId.ToString(CultureInfo.InvariantCulture)}," +
                $"{coordinates.Latitude.ToString(CultureInfo.InvariantCulture)}," +
                $"{coordinates.Longitude.ToString(CultureInfo.InvariantCulture)}");
        }
    }
}
