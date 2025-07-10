using WasabiBot.DataAccess.Repositories;
using WasabiBot.DataAccess.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Xunit;
using Npgsql;

namespace WasabiBot.IntegrationTests.DataAccess;

[Collection(nameof(TestFixture))]
public class InteractionRepositoryTests : IAsyncDisposable
{
    private readonly TestFixture _fixture;
    private readonly InteractionRepository _sut;

    public InteractionRepositoryTests(TestFixture fixture)
    {
        _fixture = fixture;
        var connection = _fixture.ServiceProvider.GetRequiredService<IDbConnection>();
        _sut = new InteractionRepository(connection);
    }

    [Fact]
    public async Task GetByIdAsync_WhenInteractionExists_ReturnsInteraction()
    {
        // Arrange
        var interaction = CreateTestInteraction();
        await _sut.CreateAsync(interaction);

        // Act
        var result = await _sut.GetByIdAsync(interaction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(interaction.Id, result.Id);
        Assert.Equal(interaction.ChannelId, result.ChannelId);
        Assert.Equal(interaction.ApplicationId, result.ApplicationId);
        Assert.Equal(interaction.UserId, result.UserId);
        Assert.Equal(interaction.GuildId, result.GuildId);
        Assert.Equal(interaction.Username, result.Username);
        Assert.Equal(interaction.GlobalName, result.GlobalName);
        Assert.Equal(interaction.Nickname, result.Nickname);
        Assert.Equal(interaction.Data, result.Data);
        Assert.True(Math.Abs((interaction.CreatedAt - result.CreatedAt).TotalSeconds) < 1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenInteractionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentId = 999999L;

        // Act
        var result = await _sut.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithValidInteraction_ReturnsTrue()
    {
        // Arrange
        var interaction = CreateTestInteraction();

        // Act
        var result = await _sut.CreateAsync(interaction);

        // Assert
        Assert.True(result);

        var savedInteraction = await _sut.GetByIdAsync(interaction.Id);
        Assert.NotNull(savedInteraction);
        Assert.Equal(interaction.Id, savedInteraction.Id);
    }

    [Fact]
    public async Task CreateAsync_WithNullGuildId_ReturnsTrue()
    {
        // Arrange
        var interaction = CreateTestInteraction(guildId: null);

        // Act
        var result = await _sut.CreateAsync(interaction);

        // Assert
        Assert.True(result);

        var savedInteraction = await _sut.GetByIdAsync(interaction.Id);
        Assert.NotNull(savedInteraction);
        Assert.Null(savedInteraction.GuildId);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_ReturnsTrue()
    {
        // Arrange
        var interaction = CreateTestInteraction(
            globalName: null,
            nickname: null,
            data: null);

        // Act
        var result = await _sut.CreateAsync(interaction);

        // Assert
        Assert.True(result);

        var savedInteraction = await _sut.GetByIdAsync(interaction.Id);
        Assert.NotNull(savedInteraction);
        Assert.Null(savedInteraction.GlobalName);
        Assert.Null(savedInteraction.Nickname);
        Assert.Null(savedInteraction.Data);
    }

    [Fact]
    public async Task CreateAsync_WithJsonData_ReturnsTrue()
    {
        // Arrange
        const string jsonData = """{"command": "test", "options": [{"name": "param", "value": "test"}]}""";
        var interaction = CreateTestInteraction(data: jsonData);

        // Act
        var result = await _sut.CreateAsync(interaction);

        // Assert
        Assert.True(result);

        var savedInteraction = await _sut.GetByIdAsync(interaction.Id);
        Assert.NotNull(savedInteraction);
        Assert.Equal(jsonData, savedInteraction.Data);
    }

    [Fact]
    public async Task CreateAsync_DuplicateId_ThrowsPostgresException()
    {
        // Arrange
        var interaction1 = CreateTestInteraction();
        var interaction2 = CreateTestInteraction(username: "DifferentUser");

        await _sut.CreateAsync(interaction1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PostgresException>(async () => await _sut.CreateAsync(interaction2));
        Assert.Equal("23505", exception.SqlState); // unique_violation
    }

    [Fact]
    public async Task CreateAsync_MultipleInteractions_AllCreatedSuccessfully()
    {
        // Arrange
        var interactions = new[]
        {
            CreateTestInteraction(id: 1001),
            CreateTestInteraction(id: 1002, username: "User2"),
            CreateTestInteraction(id: 1003, username: "User3", guildId: null)
        };

        // Act
        var results = new List<bool>();
        foreach (var interaction in interactions)
        {
            results.Add(await _sut.CreateAsync(interaction));
        }

        // Assert
        Assert.All(results, Assert.True);

        // Verify all interactions were saved
        foreach (var interaction in interactions)
        {
            var saved = await _sut.GetByIdAsync(interaction.Id);
            Assert.NotNull(saved);
            Assert.Equal(interaction.Username, saved.Username);
        }
    }

    private static InteractionEntity CreateTestInteraction(
        long id = 123456789L,
        string username = "TestUser",
        long? guildId = 987654321L,
        string? globalName = "Test Global Name",
        string? nickname = "TestNick",
        string? data = """{"name": "test", "type": 2}""")
    {
        return new InteractionEntity
        {
            Id = id,
            ChannelId = 111222333L,
            ApplicationId = 444555666L,
            UserId = 777888999L,
            GuildId = guildId,
            Username = username,
            GlobalName = globalName,
            Nickname = nickname,
            Data = data,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.ResetDatabase();
    }
}
