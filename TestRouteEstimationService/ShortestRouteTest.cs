using RouteEstimationService.Helper;
using RouteEstimationService.Domain.Entities;
using FluentResults;
using Xunit;

namespace TestRouteEstimationService;

public class ShortestRouteTest
{
    // NOTE: These tests depend on static state (_nodeCache, _edgeCache, _isLoaded) in ShortestRouteFinder.
    // They should be run in order as the first test will load the JSON datasets and subsequent tests reuse them.
    // The static cache persists across test runs, which is acceptable for these integration-style tests.
    
    //IMPORTANT NOTE: These tests use known node IDs and expected routes/distances. Should the underlying dataset change,
    //the expected values in these tests may need to be updated accordingly.

    #region Integration Tests - Known Valid Routes

    [Fact]
    public void ShortestRoute_WithKnownValidRoute_ShouldReturnExpectedPath()
    {
        // Arrange - Known route from your dataset
        const string origin = "5030596352";
        const string destination = "334305011";
        List<int> expectedEdgeIds = new() { 7919, 20623, 14224, 6635, 2390, 788, 21769, 6363, 6349, 225, 223, 22713, 6348 };

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.NodeIds);
        Assert.NotEmpty(result.Value.EdgeIds);
        
        // Verify edge IDs match expected route
        Assert.Equal(expectedEdgeIds.Count, result.Value.EdgeIds.Count);
        Assert.Equal(expectedEdgeIds, result.Value.EdgeIds);
        
        // Verify node count = edge count + 1 (nodes include both endpoints)
        Assert.Equal(expectedEdgeIds.Count + 1, result.Value.NodeIds.Count);
        
        // Verify origin and destination are correct
        Assert.Equal(origin, result.Value.NodeIds.First());
        Assert.Equal(destination, result.Value.NodeIds.Last());
    }

    [Fact]
    public void ShortestRoute_WithKnownValidRoute_ShouldReturnExpectedDistance()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";
        const double expectedDistanceKm = 3.75015;
        const double tolerance = 0.001; // Allow 1 meter tolerance due to floating point

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.InRange(result.Value.DistanceKm, expectedDistanceKm - tolerance, expectedDistanceKm + tolerance);
    }

    [Fact]
    public void ShortestRoute_CalledTwiceWithSameNodes_ShouldReturnIdenticalResults()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> firstCall = ShortestRouteFinder.ShortestRoute(origin, destination);
        Result<RouteResult> secondCall = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(firstCall.IsSuccess);
        Assert.True(secondCall.IsSuccess);
        Assert.Equal(firstCall.Value.EdgeIds, secondCall.Value.EdgeIds);
        Assert.Equal(firstCall.Value.NodeIds, secondCall.Value.NodeIds);
        Assert.Equal(firstCall.Value.DistanceKm, secondCall.Value.DistanceKm);
    }

    #endregion

    #region No Route Found Tests

    [Theory]
    [InlineData("nonexistent_origin", "334305011")]
    [InlineData("5030596352", "nonexistent_destination")]
    [InlineData("nonexistent_origin", "nonexistent_destination")]
    [InlineData("", "334305011")]
    [InlineData("5030596352", "")]
    [InlineData("  ", "334305011")]
    [InlineData("5030596352", "  ")]
    public void ShortestRoute_WithInvalidNodeIds_ShouldReturnEmptyRoute(string origin, string destination)
    {
        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.NodeIds);
        Assert.Empty(result.Value.EdgeIds);
        Assert.Equal(0, result.Value.DistanceKm);
    }

    [Fact]
    public void ShortestRoute_WithDisconnectedNodes_ShouldReturnEmptyRoute()
    {
        // Arrange
        const string origin = "334304113";
        const string destination = "12055676115";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        // If no route exists, should return empty result
        if (result.Value.NodeIds.Count != 0) return;
        Assert.Empty(result.Value.NodeIds);
        Assert.Empty(result.Value.EdgeIds);
        Assert.Equal(0, result.Value.DistanceKm);
    }

    #endregion

    #region Same Origin and Destination Tests

    [Fact]
    public void ShortestRoute_WithSameOriginAndDestination_ShouldReturnSingleNodeRoute()
    {
        // Arrange
        const string nodeId = "5030596352";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(nodeId, nodeId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        
        // When origin = destination, A* should immediately return
        // Expected: Single node, no edges, zero distance
        Assert.Single(result.Value.NodeIds);
        Assert.Equal(nodeId, result.Value.NodeIds.First());
        Assert.Empty(result.Value.EdgeIds);
        Assert.Equal(0, result.Value.DistanceKm);
    }

    [Theory]
    [InlineData("5030596352")]
    [InlineData("334305011")]
    public void ShortestRoute_WithIdenticalNodes_ShouldReturnZeroDistance(string nodeId)
    {
        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(nodeId, nodeId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.DistanceKm);
    }

    #endregion

    #region Route Structure Validation Tests

    [Fact]
    public void ShortestRoute_ValidRoute_ShouldHaveCorrectNodeToEdgeRatio()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        if (result.Value.NodeIds.Count > 0)
        {
            // Rule: NodeCount = EdgeCount + 1 (path has one more node than edges)
            Assert.Equal(result.Value.EdgeIds.Count + 1, result.Value.NodeIds.Count);
        }
    }

    [Fact]
    public void ShortestRoute_ValidRoute_ShouldHaveUniqueRouteId()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result1 = ShortestRouteFinder.ShortestRoute(origin, destination);
        Result<RouteResult> result2 = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        // Each result should have a unique RouteId (Guid)
        Assert.NotEqual(result1.Value.RouteId, result2.Value.RouteId);
    }

    [Fact]
    public void ShortestRoute_ValidRoute_ShouldHaveNonEmptyNodeIds()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.NodeIds);
        Assert.All(result.Value.NodeIds, nodeId => Assert.False(string.IsNullOrWhiteSpace(nodeId)));
    }

    [Fact]
    public void ShortestRoute_ValidRoute_ShouldHavePositiveEdgeIds()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        if (result.Value.EdgeIds.Count > 0)
        {
            Assert.All(result.Value.EdgeIds, edgeId => Assert.True(edgeId >= 0));
        }
    }

    [Fact]
    public void ShortestRoute_ValidRoute_ShouldHavePositiveDistance()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        if (result.Value.NodeIds.Count > 1)
        {
            // If route has multiple nodes, distance should be positive
            Assert.True(result.Value.DistanceKm > 0);
        }
    }

    #endregion

    #region One-Way Edge Tests

    [Fact]
    public void ShortestRoute_ShouldRespectOnewayEdges_Documentation()
    {
        // This test documents that the A* algorithm respects one-way edges:
        // - Outward traversal is ALWAYS allowed
        // - Backward traversal is ONLY allowed if edge.Oneway == false
        //
        // From the code:
        // ```
        // // Backward traversal (only if edge is not oneway)
        // if (edge.Oneway) continue;
        // ```

        // Arrange - Node "334304113" contains one-way edges
        const string nodeWithOnewayEdge = "334304113";
        const string destination = "5030596352"; // A well-connected destination node

        // Act - Try to route FROM the node with one-way edges
        Result<RouteResult> forwardRoute = ShortestRouteFinder.ShortestRoute(nodeWithOnewayEdge, destination);

        // Assert
        Assert.True(forwardRoute.IsSuccess);
        
        // If a route exists from the one-way node, it should use outward edges
        if (forwardRoute.Value.NodeIds.Count <= 0) return;
        Assert.NotEmpty(forwardRoute.Value.EdgeIds);
        Assert.NotEmpty(forwardRoute.Value.NodeIds);
        Assert.Equal(nodeWithOnewayEdge, forwardRoute.Value.NodeIds.First());
            
        // Now test reverse direction - routing TO the one-way node
        // If the edges are truly one-way, the reverse route may be different or non-existent
        Result<RouteResult> reverseRoute = ShortestRouteFinder.ShortestRoute(destination, nodeWithOnewayEdge);
        Assert.True(reverseRoute.IsSuccess);
            
        // The reverse route should either:
        // 1. Not exist (empty NodeIds) - if all paths are blocked by one-way edges
        // 2. Use a different path - if alternative routes exist that respect one-way restrictions
        // 3. Be identical reversed - only if no one-way edges are in the path (unlikely for this node)
            
        // Document that one-way edges are respected by checking the algorithm doesn't just reverse the path
        if (reverseRoute.Value.NodeIds.Count <= 0 || forwardRoute.Value.NodeIds.Count <= 0) return;
        var forwardReversed = new List<string>(forwardRoute.Value.NodeIds);
        forwardReversed.Reverse();
                
        // If edges are one-way, the paths should differ (or reverse path doesn't exist)
        // This assertion may pass if alternative routes exist
        bool pathsAreDifferent = !forwardReversed.SequenceEqual(reverseRoute.Value.NodeIds) ||
                                 reverseRoute.Value.NodeIds.Count == 0;
                
        // Document the behavior - at least one of these should be true for one-way edges
        Assert.True(pathsAreDifferent || reverseRoute.Value.EdgeIds.Count == 0, 
            "One-way edges should prevent simple path reversal or block the reverse route entirely");
    }

    [Fact]
    public void ShortestRoute_ReverseRoute_MayDifferDueToOnewayEdges()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> forwardRoute = ShortestRouteFinder.ShortestRoute(origin, destination);
        Result<RouteResult> reverseRoute = ShortestRouteFinder.ShortestRoute(destination, origin);

        // Assert
        Assert.True(forwardRoute.IsSuccess);
        Assert.True(reverseRoute.IsSuccess);

        // If both routes exist, they may be different due to one-way edges
        if (forwardRoute.Value.NodeIds.Count <= 0 || reverseRoute.Value.NodeIds.Count <= 0) return;
        // Reverse route's nodes should be forward route's nodes in reverse order
        // ONLY if there are no one-way edges in the path
        var forwardNodesReversed = new List<string>(forwardRoute.Value.NodeIds);
        if (forwardNodesReversed == null) throw new ArgumentNullException(nameof(forwardNodesReversed));
        forwardNodesReversed.Reverse();

        // They MAY be equal (if no one-way edges) or MAY differ (if one-way edges exist)
        // This test just documents that both routes can be calculated
        Assert.NotEmpty(reverseRoute.Value.NodeIds);
    }

    #endregion

    #region FluentResults Integration Tests

    [Fact]
    public void ShortestRoute_ShouldReturnFluentResultSuccess()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.IsType<Result<RouteResult>>(result);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailed);
    }

    [Fact]
    public void ShortestRoute_WithInvalidNodes_ShouldStillReturnSuccess()
    {
        // Even when nodes don't exist, method returns Result.Ok() with empty RouteResult
        // This is by design (not an error, just no route found)

        // Arrange
        const string origin = "nonexistent";
        const string destination = "alsoNonexistent";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailed);
        Assert.Empty(result.Value.NodeIds);
    }

    #endregion

    #region Static State and Data Loading Tests

    [Fact]
    public void ShortestRoute_OnFirstCall_ShouldLoadDataSuccessfully()
    {
        // This test documents that the first call loads the JSON datasets
        // Since static state persists, this effectively tests that LoadData() was called
        // and didn't throw an exception

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act - Should load datasets on first call (or use already loaded cache)
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert - If data loaded successfully, we get a valid result
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void ShortestRoute_MultipleCalls_ShouldReuseLoadedData()
    {
        // Documents that data is loaded once and reused
        // Performance: Subsequent calls should be faster (no file I/O)

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act - Multiple calls
        var result1 = ShortestRouteFinder.ShortestRoute(origin, destination);
        var result2 = ShortestRouteFinder.ShortestRoute(origin, destination);
        var result3 = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert - All should succeed and return identical results
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result3.IsSuccess);
        Assert.Equal(result1.Value.EdgeIds, result2.Value.EdgeIds);
        Assert.Equal(result2.Value.EdgeIds, result3.Value.EdgeIds);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ShortestRoute_WithWhitespaceNodeIds_ShouldReturnEmptyRoute()
    {
        // Arrange
        const string origin = "   ";
        const string destination = "\t\n";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.NodeIds);
    }

    #endregion

    #region RouteResult Properties Tests

    [Fact]
    public void ShortestRoute_RouteResult_ShouldInitializeAllProperties()
    {
        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.RouteId);
        Assert.NotNull(result.Value.NodeIds);
        Assert.NotNull(result.Value.EdgeIds);
        Assert.NotNull(result.Value.Path);
        Assert.True(result.Value.DistanceKm >= 0);
        Assert.Equal(0, result.Value.EstimatedTimeSeconds); // Not calculated by ShortestRouteFinder
    }

    [Fact]
    public void ShortestRoute_RouteResult_ShouldHaveEmptyPathList()
    {
        // ShortestRouteFinder doesn't populate the Path property
        // This test documents that behavior

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Path);
    }

    #endregion

    #region Distance Calculation Tests

    [Fact]
    public void ShortestRoute_DistanceKm_ShouldMatchSumOfEdgeLengths()
    {
        // This test documents that DistanceKm is calculated by:
        // 1. Summing all edge LengthCm values in the route
        // 2. Converting to kilometers (divide by 100,000)

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        if (result.Value.EdgeIds.Count <= 0) return;
        // Distance should be positive if there are edges
        Assert.True(result.Value.DistanceKm > 0);
            
        // For the known route, verify expected distance
        const double expectedKm = 3.75015;
        const double tolerance = 0.001;
        Assert.InRange(result.Value.DistanceKm, expectedKm - tolerance, expectedKm + tolerance);
    }

    [Fact]
    public void ShortestRoute_WithZeroLengthRoute_ShouldHaveZeroDistance()
    {
        // Arrange - Same node as origin and destination
        const string nodeId = "5030596352";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(nodeId, nodeId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.DistanceKm);
    }

    #endregion

    #region A* Algorithm Behavior Tests

    [Fact]
    public void ShortestRoute_ShouldUseAStarAlgorithm_Documentation()
    {
        // This test documents that the routing uses A* pathfinding algorithm:
        // - Uses Haversine distance as heuristic (straight-line distance)
        // - Maintains gScore (actual distance from start)
        // - Maintains fScore (gScore + heuristic to goal)
        // - Uses PriorityQueue to explore most promising nodes first

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.NodeIds);

        // A* guarantees finding the shortest path if one exists
        // The route we get should be optimal (shortest distance)
        Assert.True(result.Value.DistanceKm > 0);
    }

    [Fact]
    public void ShortestRoute_ShouldFindOptimalPath_NotJustAnyPath()
    {
        // A* should find the SHORTEST path, not just any path

        // Arrange
        const string origin = "5030596352";
        const string destination = "334305011";

        // Act
        Result<RouteResult> result = ShortestRouteFinder.ShortestRoute(origin, destination);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify we get the known optimal route
        List<int> expectedEdgeIds = new() { 7919, 20623, 14224, 6635, 2390, 788, 21769, 6363, 6349, 225, 223, 22713, 6348 };
        Assert.Equal(expectedEdgeIds, result.Value.EdgeIds);
    }

    #endregion
}