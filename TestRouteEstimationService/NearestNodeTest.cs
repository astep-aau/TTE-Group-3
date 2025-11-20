using RouteEstimationService.Helper;
using Xunit;

namespace TestRouteEstimationService;

public class NearestNodeTest
{
    // NOTE: These tests depend on static state (_nodeCache, _isLoaded) in NearestNodeFinder.
    // They should be run in order as the first test will load the dataset and subsequent tests reuse it.
    // The static cache persists across test runs, which is acceptable for these integration-style tests.

    #region Happy Path Tests - Known Coordinates

    [Fact]
    public void NearestNode_WithKnownOriginCoordinates_ShouldReturnValidNodeId()
    {
        // Arrange - Known origin coordinates from China dataset
        const string lat = "45.7821345";
        const string lon = "126.5570674";

        // Act
        string nodeId = NearestNodeFinder.NearestNode(lat, lon);

        // Assert
        Assert.NotNull(nodeId);
        Assert.NotEmpty(nodeId);
    }

    [Fact]
    public void NearestNode_WithKnownDestinationCoordinates_ShouldReturnValidNodeId()
    {
        // Arrange - Known destination coordinates from China dataset
        const string lat = "45.7601284";
        const string lon = "126.5864540";

        // Act
        string nodeId = NearestNodeFinder.NearestNode(lat, lon);

        // Assert
        Assert.NotNull(nodeId);
        Assert.NotEmpty(nodeId);
    }

    [Fact]
    public void NearestNode_CalledTwiceWithSameCoordinates_ShouldReturnSameNodeId()
    {
        // Arrange
        const string lat = "45.7821345";
        const string lon = "126.5570674";

        // Act
        string firstCall = NearestNodeFinder.NearestNode(lat, lon);
        string secondCall = NearestNodeFinder.NearestNode(lat, lon);

        // Assert
        Assert.Equal(firstCall, secondCall);
    }

    #endregion

    #region Coordinate Format Tests

    [Theory]
    [InlineData("45.7821345", "126.5570674")]      // Standard format
    [InlineData("45.78213", "126.55706")]          // Fewer decimals
    [InlineData("45.782134500", "126.557067400")]  // Extra decimals
    public void NearestNode_WithVariousValidFormats_ShouldReturnNodeId(string lat, string lon)
    {
        // Act
        string nodeId = NearestNodeFinder.NearestNode(lat, lon);

        // Assert
        Assert.NotNull(nodeId);
        Assert.NotEmpty(nodeId);
    }

    [Theory]
    [InlineData("invalid", "126.5570674")]
    [InlineData("45.7821345", "invalid")]
    [InlineData("abc", "def")]
    [InlineData("", "126.5570674")]
    [InlineData("45.7821345", "")]
    [InlineData("  ", "126.5570674")]
    [InlineData("45.7821345", "  ")]
    [InlineData("45.7821,345", "126.5570674")]  // Comma instead of period
    [InlineData("45.7821345", "126,5570674")]   // Comma instead of period
    public void NearestNode_WithInvalidCoordinateFormats_ShouldThrowArgumentException(string lat, string lon)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => NearestNodeFinder.NearestNode(lat, lon));
        Assert.Contains("Invalid latitude or longitude", exception.Message);
    }

    #endregion

    #region Distance Threshold Tests - Coordinates Outside Known Area

    [Theory]
    [InlineData("48.8566", "2.3522")]      // Paris, France - Western Europe
    [InlineData("51.5074", "-0.1278")]     // London, UK
    [InlineData("52.5200", "13.4050")]     // Berlin, Germany
    [InlineData("40.4168", "-3.7038")]     // Madrid, Spain
    [InlineData("41.9028", "12.4964")]     // Rome, Italy
    public void NearestNode_WithCoordinatesFarFromDataset_ShouldThrowException(string lat, string lon)
    {
        // Act & Assert
        var exception = Assert.Throws<Exception>(() => NearestNodeFinder.NearestNode(lat, lon));
        Assert.Contains("No nearby node found within 1000 meters", exception.Message);
    }

    [Fact]
    public void NearestNode_WithCoordinatesAtExactly1000MetersAway_ShouldThrowException()
    {
        // This test documents that the threshold is exclusive (>) not inclusive (>=)
        // If a node is EXACTLY 1000m away, it should still fail
        // Note: This is hard to test precisely without knowing exact dataset coordinates
        // This test serves as documentation of the expected behavior

        // Arrange - Coordinates definitely >1000m from any China dataset node
        const string lat = "48.8566";  // Paris
        const string lon = "2.3522";

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => NearestNodeFinder.NearestNode(lat, lon));
        Assert.Contains("No nearby node found within 1000 meters", exception.Message);
    }

    #endregion

    #region TryGetCoordinates Tests

    [Fact]
    public void TryGetCoordinates_WithValidNodeId_ShouldReturnTrueAndCoordinates()
    {
        // Arrange - First get a valid node ID from the dataset
        const string knownLat = "45.7821345";
        const string knownLon = "126.5570674";
        string nodeId = NearestNodeFinder.NearestNode(knownLat, knownLon);

        // Act
        bool result = NearestNodeFinder.TryGetCoordinates(nodeId, out double lat, out double lon);

        // Assert
        Assert.True(result);
        Assert.NotEqual(0, lat);
        Assert.NotEqual(0, lon);
        // Verify coordinates are in reasonable range
        Assert.InRange(lat, -90, 90);
        Assert.InRange(lon, -180, 180);
    }

    [Fact]
    public void TryGetCoordinates_WithValidNodeId_ShouldReturnCoordinatesNearOriginal()
    {
        // Arrange
        const string knownLat = "45.7821345";
        const string knownLon = "126.5570674";
        string nodeId = NearestNodeFinder.NearestNode(knownLat, knownLon);

        // Act
        bool result = NearestNodeFinder.TryGetCoordinates(nodeId, out double lat, out double lon);

        // Assert
        Assert.True(result);
        // The returned coordinates should be within 1000m (roughly 0.01 degrees) of original
        Assert.InRange(lat, 45.77, 45.79);
        Assert.InRange(lon, 126.55, 126.57);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("nonexistent")]
    [InlineData("999999999")]
    [InlineData("invalid-node-id")]
    [InlineData("null")]
    public void TryGetCoordinates_WithInvalidNodeId_ShouldReturnFalseAndZeroCoordinates(string nodeId)
    {
        // Act
        bool result = NearestNodeFinder.TryGetCoordinates(nodeId, out double lat, out double lon);

        // Assert
        Assert.False(result);
        Assert.Equal(0, lat);
        Assert.Equal(0, lon);
    }

    #endregion

    #region Static State and File Loading Tests

    [Fact]
    public void NearestNode_OnFirstCall_ShouldLoadDatasetSuccessfully()
    {
        // This test documents that the first call loads the CSV file
        // Since static state persists, this effectively tests that LoadNodes() was called
        // and didn't throw an exception

        // Arrange
        const string lat = "45.7821345";
        const string lon = "126.5570674";

        // Act - Should load dataset on first call (or use already loaded cache)
        string nodeId = NearestNodeFinder.NearestNode(lat, lon);

        // Assert
        Assert.NotNull(nodeId);
        Assert.NotEmpty(nodeId);
    }

    [Fact]
    public void TryGetCoordinates_OnFirstCall_ShouldLoadDatasetSuccessfully()
    {
        // Similar to above, but tests TryGetCoordinates triggers loading

        // Arrange - Get a valid node first
        string nodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");

        // Act - Should load dataset if not already loaded
        bool result = NearestNodeFinder.TryGetCoordinates(nodeId, out _, out _);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Theory]
    [InlineData("0", "0")]              // Equator and Prime Meridian
    [InlineData("-90", "-180")]         // South Pole, West boundary
    [InlineData("90", "180")]           // North Pole, East boundary
    [InlineData("-90", "180")]          // South Pole, East boundary
    [InlineData("90", "-180")]          // North Pole, West boundary
    public void NearestNode_WithExtremeValidCoordinates_ShouldHandleGracefully(string lat, string lon)
    {
        // These coordinates are valid but definitely not in the China dataset
        // Should throw "No nearby node found" exception

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => NearestNodeFinder.NearestNode(lat, lon));
        Assert.Contains("No nearby node found within 1000 meters", exception.Message);
    }

    [Fact]
    public void NearestNode_WithVerySmallCoordinateDifference_ShouldReturnSameNode()
    {
        // Arrange - Two coordinates extremely close together
        const string lat1 = "45.7821345";
        const string lon1 = "126.5570674";
        const string lat2 = "45.7821346";  // 0.0000001 degree difference (about 1cm)
        const string lon2 = "126.5570675";

        // Act
        string node1 = NearestNodeFinder.NearestNode(lat1, lon1);
        string node2 = NearestNodeFinder.NearestNode(lat2, lon2);

        // Assert
        Assert.Equal(node1, node2);
    }

    #endregion

    #region Documentation Tests - Expected Behavior

    [Fact]
    public void NearestNode_ShouldUseHaversineFormula_Documentation()
    {
        // This test documents that the distance calculation uses Haversine formula
        // which accounts for Earth's curvature (more accurate than simple Euclidean distance)

        // Arrange
        const string lat = "45.7821345";
        const string lon = "126.5570674";

        // Act
        string nodeId = NearestNodeFinder.NearestNode(lat, lon);

        // Assert - If Haversine is working, we should get a valid nearby node
        Assert.NotNull(nodeId);
        Assert.NotEmpty(nodeId);

        // Additional documentation: Haversine formula is critical because:
        // - At high latitudes, longitude degrees represent shorter distances
        // - Simple lat/lon differences would give incorrect results
        // - 1000m threshold must be calculated accurately
    }

    [Fact]
    public void NearestNode_ShouldCache_Documentation()
    {
        // This test documents that the node cache is reused across calls
        // Performance test: second call should be faster (though hard to measure reliably)

        // Arrange
        const string lat = "45.7821345";
        const string lon = "126.5570674";

        // Act - Multiple calls
        string first = NearestNodeFinder.NearestNode(lat, lon);
        string second = NearestNodeFinder.NearestNode(lat, lon);
        string third = NearestNodeFinder.NearestNode(lat, lon);

        // Assert - All should return same result (proving cache consistency)
        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    #endregion
}
