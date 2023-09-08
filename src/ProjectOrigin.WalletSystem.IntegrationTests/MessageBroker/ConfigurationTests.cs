using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Options;
using Xunit;

namespace ProjectOrigin.WalletSystem.IntegrationTests.MessageBroker;

public class ConfigurationTests
{
    private const string SectionName = "MessageBroker";

    [Fact]
    public void MissingConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        Action act = () => configuration.GetSection(SectionName).GetValid<MessageBrokerOptions>();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void InvalidType()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = "InvalidType",
            })
            .Build();

        // Act
        Action act = () => configuration.GetSection(SectionName).GetValid<MessageBrokerOptions>();

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void InMemoryValid()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = MessageBrokerType.InMemory.ToString(),
            })
            .Build();

        // Act
        var options = configuration.GetSection(SectionName).GetValid<MessageBrokerOptions>();

        // Assert
        options.Should().NotBeNull();
        options.Type.Should().Be(MessageBrokerType.InMemory);
        options.RabbitMq.Should().BeNull();
    }

    [Fact]
    public void InMemoryWierdCaseValid()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = "iNMeMory",
            })
            .Build();

        // Act
        var options = configuration.GetSection(SectionName).GetValid<MessageBrokerOptions>();

        // Assert
        options.Should().NotBeNull();
        options.Type.Should().Be(MessageBrokerType.InMemory);
        options.RabbitMq.Should().BeNull();
    }

    [Fact]
    public void RabbitMqValid()
    {
        // Arrange
        var fixture = new Fixture();
        var host = "localhost";
        ushort port = 5672;
        var username = fixture.Create<string>();
        var password = fixture.Create<string>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = MessageBrokerType.RabbitMq.ToString(),
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Host)}"] = host,
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Port)}"] = port.ToString(),
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Username)}"] = username,
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Password)}"] = password,
            })
            .Build();

        // Act
        var options = configuration.GetSection(SectionName).GetValid<MessageBrokerOptions>();

        // Assert
        options.Should().NotBeNull();
        options.Type.Should().Be(MessageBrokerType.RabbitMq);
        options.RabbitMq.Should().NotBeNull();
        options.RabbitMq!.Host.Should().Be(host);
        options.RabbitMq!.Port.Should().Be(port);
        options.RabbitMq!.Username.Should().Be(username);
        options.RabbitMq!.Password.Should().Be(password);
    }

    [Fact]
    public void RabbitMqMissingConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = MessageBrokerType.RabbitMq.ToString(),
            })
            .Build();

        // Act
        Action act = () => configuration.GetSection($"{SectionName}").GetValid<MessageBrokerOptions>();

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RabbitMqMisconfiguredUrl()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = MessageBrokerType.RabbitMq.ToString(),
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Host)}"] = "",
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Port)}"] = "5672",
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Username)}"] = "guest",
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Password)}"] = "guest",
            })
            .Build();

        // Act
        Action act = () => configuration.GetSection($"{SectionName}").GetValid<MessageBrokerOptions>();

        // Assert
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RabbitMqMissingFields()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SectionName}:{nameof(MessageBrokerOptions.Type)}"] = MessageBrokerType.RabbitMq.ToString(),
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Host)}"] = "localhost",
                [$"{SectionName}:{nameof(MessageBrokerOptions.RabbitMq)}:{nameof(RabbitMqOptions.Password)}"] = "guest",
            })
            .Build();

        // Act
        Action act = () => configuration.GetSection($"{SectionName}").GetValid<MessageBrokerOptions>();

        // Assert
        act.Should().Throw<ValidationException>();
    }
}
