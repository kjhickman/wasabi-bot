using System.Data;
using Discord;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Repositories;

namespace WasabiBot.Tests.Integration.DataAccess.Repositories;

[Collection(nameof(PostgresTestCollectionFixture))]
public class InteractionRecordRepositoryTests
{
    private readonly InteractionRecordRepository _sut;
    
    public InteractionRecordRepositoryTests(PostgresTestFixture fixture)
    {
        var connection = fixture.ServiceProvider.GetRequiredService<IDbConnection>();
        var tracer = fixture.ServiceProvider.GetRequiredService<Tracer>();
        _sut = new InteractionRecordRepository(connection, tracer);
    }
    
    [Fact]
    public async Task Test()
    {
        // Arrange
        var interactionRecord = new InteractionRecord
        {
            Id = Random.Shared.NextInt64(),
            Type = (int)InteractionType.ApplicationCommand,
            UserId = Random.Shared.NextInt64().ToString(),
            Username = "wasabibot",
            UserGlobalName = "wasabibot#0000",
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            InsertedAt = default
        };

        // Act
        var result = await _sut.CreateAsync(interactionRecord);
        
        // Assert
        result.Should().BeTrue();
    }
}