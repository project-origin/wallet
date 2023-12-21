using FluentAssertions;
using ProjectOrigin.HierarchicalDeterministicKeys.Implementations;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Extensions;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Extensions;


public class IHDAlgorithmExtensionsTests
{
    [Fact]
    public void Valid()
    {
        // Arrange
        var hdAlgorithm = new Secp256k1Algorithm();
        var privateKeyBytes = hdAlgorithm.GenerateNewPrivateKey().Export().ToArray();

        // Act
        var result = hdAlgorithm.TryImportHDPrivateKey(privateKeyBytes, out var hdPrivateKey);

        // Assert
        result.Should().BeTrue();
        hdPrivateKey.Should().NotBeNull();
        hdPrivateKey.Should().BeAssignableTo<IHDPrivateKey>();
    }

    [Fact]
    public void Invalid()
    {
        // Arrange
        var hdAlgorithm = new Secp256k1Algorithm();
        var privateKeyBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = hdAlgorithm.TryImportHDPrivateKey(privateKeyBytes, out var hdPrivateKey);

        // Assert
        result.Should().BeFalse();
        hdPrivateKey.Should().BeNull();
    }
}
