using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;
using Xunit;

namespace WasabiBot.IntegrationTests.DataAccess;

[Collection(nameof(TestFixture))]
public class InteractionServiceTests : IAsyncDisposable
{
    private readonly TestFixture _fixture;

    public InteractionServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByIdAsync_WhenInteractionExists_ReturnsInteraction()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var interaction = CreateTestInteraction();
        await service.CreateAsync(interaction);

        var result = await service.GetByIdAsync(interaction.Id);

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
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var result = await service.GetByIdAsync(999999L);
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithValidInteraction_ReturnsTrue()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var interaction = CreateTestInteraction();
        var result = await service.CreateAsync(interaction);

        Assert.True(result);
        var saved = await service.GetByIdAsync(interaction.Id);
        Assert.NotNull(saved);
        Assert.Equal(interaction.Id, saved.Id);
    }

    [Fact]
    public async Task CreateAsync_WithNullGuildId_ReturnsTrue()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var interaction = CreateTestInteraction(guildId: null);
        var result = await service.CreateAsync(interaction);

        Assert.True(result);
        var saved = await service.GetByIdAsync(interaction.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.GuildId);
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalFields_ReturnsTrue()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var interaction = CreateTestInteraction(globalName: null, nickname: null, data: null);
        var result = await service.CreateAsync(interaction);

        Assert.True(result);
        var saved = await service.GetByIdAsync(interaction.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.GlobalName);
        Assert.Null(saved.Nickname);
        Assert.Null(saved.Data);
    }

    [Fact]
    public async Task CreateAsync_WithJsonData_ReturnsTrue()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        const string jsonData = """{""command": ""test"", ""options"": [{""name"": ""param"", ""value"": ""test""}]}""";
        var interaction = CreateTestInteraction(data: jsonData);
        var result = await service.CreateAsync(interaction);

        Assert.True(result);
        var saved = await service.GetByIdAsync(interaction.Id);
        Assert.NotNull(saved);
        Assert.Equal(jsonData, saved.Data);
    }

    [Fact]
    public async Task CreateAsync_DuplicateId_ThrowsDbUpdateException()
    {
        // First insert
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<InteractionService>();
            await service.CreateAsync(CreateTestInteraction());
        }
        // Second insert with different values but same id in new context
        using (var scope = _fixture.ServiceProvider.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<InteractionService>();
            var duplicate = CreateTestInteraction(username: "DifferentUser");
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(duplicate));
            Assert.IsType<DbUpdateException>(ex);
            if (ex.InnerException is PostgresException pg)
            {
                Assert.Equal("23505", pg.SqlState); // unique_violation
            }
        }
    }

    [Fact]
    public async Task CreateAsync_MultipleInteractions_AllCreatedSuccessfully()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InteractionService>();

        var interactions = new[]
        {
            CreateTestInteraction(id: 1001),
            CreateTestInteraction(id: 1002, username: "User2"),
            CreateTestInteraction(id: 1003, username: "User3", guildId: null)
        };

        foreach (var interaction in interactions)
        {
            var created = await service.CreateAsync(interaction);
            Assert.True(created);
        }

        foreach (var interaction in interactions)
        {
            var saved = await service.GetByIdAsync(interaction.Id);
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
        string? data = """{""name"": ""test"", ""type"": 2}""")
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
