using System.Text.Json;
using Discord;
using Shouldly;
using WasabiBot.DataAccess.Entities;
using WasabiBot.Discord.Api;

namespace WasabiBot.Tests.Unit.DataAccess.Entities;

public class InteractionRecordTests
{
    [Fact]
    public void FromInteractionJson_ShouldThrow_WhenInvalidJson()
    {
        const string json = "{}";
        
        var result = () => InteractionRecord.FromInteractionJson(json);
        
        result.ShouldThrow<JsonException>();
    }
    
    [Fact]
    public void FromInteractionJson_WithValidJson_ShouldDeserializeAndMapCorrectly()
    {
        // Arrange
        var id = Random.Shared.NextInt64();
        var userId = Random.Shared.NextInt64().ToString();
        var username = "JsonUser";
        var globalName = "JsonGlobalName";
        var discriminator = "1234";
        var interaction = new Interaction
        {
            Id = id.ToString(),
            Type = InteractionType.ApplicationCommand,
            User = new User
            {
                Id = userId,
                Username = username,
                GlobalName = globalName,
                Discriminator = discriminator
            },
            Version = 1,
            ApplicationId = Random.Shared.NextInt64().ToString(),
            Token = "test-token",
        };
        var json = JsonSerializer.Serialize(interaction);

        // Act
        var result = InteractionRecord.FromInteractionJson(json);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Type.ShouldBe((int)InteractionType.ApplicationCommand);
        result.UserId.ShouldBe(userId);
        result.Username.ShouldBe(username);
        result.UserGlobalName.ShouldBe(globalName);
        result.Version.ShouldBe(1);
    }

    [Fact]
    public void FromInteraction_WithFullData_ShouldMapAllProperties()
    {
        // Arrange
        var interaction = new Interaction
        {
            Id = "123456789012345678",
            Type = InteractionType.ApplicationCommand,
            Data = new DiscordInteractionData
            {
                Id = "987654321",
                Name = "test-command"
            },
            GuildId = "guild123",
            ChannelId = "channel456",
            GuildMember = new GuildMember
            {
                Nickname = "TestNick",
                AvatarHash = "abc123",
                RoleIds = ["role1", "role2"],
                JoinedAt = DateTimeOffset.UtcNow,
                PremiumSince = DateTimeOffset.UtcNow.AddDays(-30),
                Deafened = true,
                Muted = false,
                Permissions = "12345",
                User = new User
                {
                    Id = "user789",
                    Username = "TestUser",
                    GlobalName = "TestGlobalName",
                    Discriminator = "1234"
                }
            },
            Version = 1,
            ApplicationId = Random.Shared.NextInt64().ToString(),
            Token = "test-token"
        };

        // Act
        var result = InteractionRecord.FromInteraction(interaction);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(123456789012345678);
        result.Type.ShouldBe((int)InteractionType.ApplicationCommand);
        result.Data.ShouldNotBeNull();
        result.Data.ShouldContain("test-command");
        result.GuildId.ShouldBe("guild123");
        result.ChannelId.ShouldBe("channel456");
        result.MemberNickname.ShouldBe("TestNick");
        result.MemberAvatarHash.ShouldBe("abc123");
        result.MemberRoleIds.ShouldBeEquivalentTo(new[] { "role1", "role2" });
        result.MemberJoinedAt.ShouldBe(interaction.GuildMember.JoinedAt);
        result.MemberPremiumSince.ShouldBe(interaction.GuildMember.PremiumSince);
        result.MemberDeafened.ShouldBe(true);
        result.MemberMuted.ShouldBe(false);
        result.MemberPermissions.ShouldBe("12345");
        result.UserId.ShouldBe("user789");
        result.Username.ShouldBe("TestUser");
        result.UserGlobalName.ShouldBe("TestGlobalName");
        result.Version.ShouldBe(1);
        result.CreatedAt.ShouldNotBe(default);
        result.InsertedAt.ShouldBe(DateTimeOffset.MinValue);
    }

    [Fact]
    public void FromInteraction_WithGuildMemberUser_ShouldUseGuildMemberUserProperties()
    {
        // Arrange
        var interaction = new Interaction
        {
            Id = "123456789012345678",
            Type = InteractionType.ApplicationCommand,
            GuildMember = new GuildMember
            {
                User = new User
                {
                    Id = "guildUser123",
                    Username = "GuildUser",
                    GlobalName = "GuildGlobalName",
                    Discriminator = "1234"
                },
                RoleIds = []
            },
            Version = 1,
            ApplicationId = Random.Shared.NextInt64().ToString(),
            Token = "test-token"
        };

        // Act
        var result = InteractionRecord.FromInteraction(interaction);

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBe("guildUser123");
        result.Username.ShouldBe("GuildUser");
        result.UserGlobalName.ShouldBe("GuildGlobalName");
    }
}