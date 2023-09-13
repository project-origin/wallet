using AutoFixture;
using FluentAssertions;
using ProjectOrigin.WalletSystem.IntegrationTests.TestClassFixtures;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Repositories;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.Repositories;

public class RegistryRepositoryTests : AbstractRepositoryTests
{
    private readonly RegistryRepository _repository;

    public RegistryRepositoryTests(PostgresDatabaseFixture dbFixture) : base(dbFixture)
    {
        _repository = new RegistryRepository(_connection);
    }

    [Fact]
    public async Task InsertRegistry_InsertsRegistry()
    {
        // Arrange
        var registry = _fixture.Create<RegistryModel>();

        // Act
        await _repository.InsertRegistry(registry);

        // Assert
        var insertedRegistry = await _repository.GetRegistryFromName(registry.Name);
        insertedRegistry.Should().BeEquivalentTo(registry);
    }

    [Fact]
    public async Task GetRegistryFromName_ReturnsRegistry()
    {
        // Arrange
        var registry = await CreateRegistry();

        // Act
        var result = await _repository.GetRegistryFromName(registry.Name);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(registry);
    }

    [Fact]
    public async Task GetRegistryFromId_ReturnsRegistry()
    {
        // Arrange
        var registry = await CreateRegistry();

        // Act
        var result = await _repository.GetRegistryFromId(registry.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(registry);
    }
}
