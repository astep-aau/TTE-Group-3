using RouteEstimationService.Helper;
using FluentAssertions;

namespace TestRouteEstimationService
{
    public class NodeIdToCoordinatesTest
    {
        // Runs with the following command: dotnet test --filter "FullyQualifiedName~NodeIdToCoordinatesTest"
        // The first call to Map will trigger loading of the vertex.csv dataset.
        // Subsequent tests reuse the loaded cache.

        #region Happy Path Tests - Valid Node IDs

        // Single valid Node ID returns coordinates
        [Fact]
        public void Map_WithValidSingleNodeId_ShouldReturnCorrectCoordinates()
        {
            // Arrange
            string validNodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            var nodeIds = new List<string> { validNodeId };

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Lat.Should().NotBe(0);
            result[0].Lon.Should().NotBe(0);
        }

        // Single specific valid Node ID returns the coordinates of that specific node
        [Fact]
        public void Map_WithValidSingleNodeIdSpecific_ShouldReturnSpecificCoordinates()
        {
            // Arrange
            // Find a specific node ID from the dataset
            string validNodeId = NearestNodeFinder.NearestNode("45.7814988", "126.5576157");
            var nodeIds = new List<string> { validNodeId };

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            // Same checks as before, but with known expected values
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Lat.Should().Be(45.7814988);
            result[0].Lon.Should().Be(126.5576157);
        }
        
        // Multiple valid Node IDs return their coordinates
        [Fact]
        public void Map_WithMultipleValidNodeIds_ShouldReturnAllCoordinates()
        {
            // Arrange
            string nodeId1 = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            string nodeId2 = NearestNodeFinder.NearestNode("45.7601284", "126.5864540");
            var nodeIds = new List<string> { nodeId1, nodeId2 };

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result[0].Lat.Should().NotBe(0);
            result[0].Lon.Should().NotBe(0);
            result[1].Lat.Should().NotBe(0);
            result[1].Lon.Should().NotBe(0);
        }

        // Empty list of Node IDs returns empty list of coordinates
        [Fact]
        public void Map_WithEmptyList_ShouldReturnEmptyList()
        {
            // Arrange
            var nodeIds = new List<string>();
            if (nodeIds == null) throw new ArgumentNullException(nameof(nodeIds));

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // Consistent results for same Node ID on multiple calls
        [Fact]
        public void Map_CalledTwiceWithSameNodeIds_ShouldReturnConsistentResults()
        {
            // Arrange
            string validNodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            var nodeIds = new List<string> { validNodeId };

            // Act
            var result1 = NodeIdToCoordinates.Map(nodeIds);
            var result2 = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result1[0].Lat.Should().Be(result2[0].Lat);
            result1[0].Lon.Should().Be(result2[0].Lon);
        }

        // Duplicate of the same Node IDs return the same coordinates multiple times
        [Fact]
        public void Map_WithDuplicateNodeIds_ShouldReturnDuplicateCoordinates()
        {
            // Arrange
            string validNodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            var nodeIds = new List<string> { validNodeId, validNodeId };

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result.Should().HaveCount(2);
            result[0].Lat.Should().Be(result[1].Lat);
            result[0].Lon.Should().Be(result[1].Lon);
        }

        #endregion

        #region Error Handling Tests

        // Invalid Node ID throws KeyNotFoundException
        [Fact]
        public void Map_WithInvalidNodeId_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            var nodeIds = new List<string> { "invalid_node_id" };

            // Act
            Action act = () => NodeIdToCoordinates.Map(nodeIds);

            // Assert
            act.Should().Throw<KeyNotFoundException>()
                .WithMessage("*invalid_node_id*");
        }

        // Mixed valid and invalid Node IDs throws KeyNotFoundException
        [Fact]
        public void Map_WithMixedValidAndInvalidNodeIds_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            string validNodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            var nodeIds = new List<string> { validNodeId, "invalid_node" };

            // Act
            Action act = () => NodeIdToCoordinates.Map(nodeIds);

            // Assert
            act.Should().Throw<KeyNotFoundException>()
                .WithMessage("*invalid_node*");
        }

        #endregion

        #region Edge Cases

        // Large number of Node IDs processes without error
        [Fact]
        public void Map_WithLargeNumberOfNodeIds_ShouldReturnAllCoordinates()
        {
            // Arrange
            string validNodeId = NearestNodeFinder.NearestNode("45.7821345", "126.5570674");
            var nodeIds = new List<string>();
            for (int i = 0; i < 200; i++)
            {
                nodeIds.Add(validNodeId);
            }

            // Act
            var result = NodeIdToCoordinates.Map(nodeIds);

            // Assert
            result.Should().HaveCount(200);
            result.Should().OnlyContain(coord => coord.Lat != 0 && coord.Lon != 0);
        }

        #endregion
    }
}

