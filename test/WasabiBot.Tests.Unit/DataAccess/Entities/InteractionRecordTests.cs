using System.Text.Json;
using Discord;
using FluentAssertions;
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
        
        result.Should().Throw<JsonException>();
    }
    
    [Fact]
    public void FromInteractionJson_WithValidJson_ShouldDeserializeAndMapCorrectly()
    {
        // Arrange
        var interaction = new Interaction
        {
            Id = "123456789012345678",
            Type = InteractionType.ApplicationCommand,
            User = new User
            {
                Id = "user123",
                Username = "JsonUser",
                GlobalName = "JsonGlobalName"
            },
            Version = 1
        };
        var json = JsonSerializer.Serialize(interaction);

        // Act
        var result = InteractionRecord.FromInteractionJson(json);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789012345678);
        result.Type.Should().Be((int)InteractionType.ApplicationCommand);
        result.UserId.Should().Be("user123");
        result.Username.Should().Be("JsonUser");
        result.UserGlobalName.Should().Be("JsonGlobalName");
        result.Version.Should().Be(1);
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
                    GlobalName = "TestGlobalName"
                }
            },
            Version = 1
        };

        // Act
        var result = InteractionRecord.FromInteraction(interaction);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789012345678);
        result.Type.Should().Be((int)InteractionType.ApplicationCommand);
        result.Data.Should().NotBeNull();
        result.Data.Should().Contain("test-command");
        result.GuildId.Should().Be("guild123");
        result.ChannelId.Should().Be("channel456");
        result.MemberNickname.Should().Be("TestNick");
        result.MemberAvatarHash.Should().Be("abc123");
        result.MemberRoleIds.Should().BeEquivalentTo("role1", "role2");
        result.MemberJoinedAt.Should().Be(interaction.GuildMember.JoinedAt);
        result.MemberPremiumSince.Should().Be(interaction.GuildMember.PremiumSince);
        result.MemberDeafened.Should().BeTrue();
        result.MemberMuted.Should().BeFalse();
        result.MemberPermissions.Should().Be("12345");
        result.UserId.Should().Be("user789");
        result.Username.Should().Be("TestUser");
        result.UserGlobalName.Should().Be("TestGlobalName");
        result.Version.Should().Be(1);
        result.CreatedAt.Should().NotBe(default);
        result.InsertedAt.Should().Be(DateTimeOffset.MinValue);
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
                    GlobalName = "GuildGlobalName"
                },
                RoleIds = []
            },
            Version = 1
        };

        // Act
        var result = InteractionRecord.FromInteraction(interaction);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be("guildUser123");
        result.Username.Should().Be("GuildUser");
        result.UserGlobalName.Should().Be("GuildGlobalName");
    }
}