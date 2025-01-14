using Discord;
using FluentAssertions;
using WasabiBot.Discord.Modules;

namespace WasabiBot.Tests.Unit.Discord.Modules;

public class MagicConchModuleTests
{
    [Fact]
    public void BuildMagicConchResponse_ShouldIncludeUserMentionAndQuestion()
    {
        // Arrange
        var question = "Will I pass my exam?";
        var mention = "<@123456789>";

        // Act
        var response = MagicConchModule.BuildMagicConchResponse(question, mention);

        // Assert
        response.Should().StartWith($"{mention} asked");
        response.Should().Contain(Format.Italics(question));
    }

    [Fact]
    public void BuildMagicConchResponse_ShouldIncludeMagicConchAnswer()
    {
        // Arrange
        var question = "Will it rain tomorrow?";
        var mention = "<@987654321>";

        // Act
        var response = MagicConchModule.BuildMagicConchResponse(question, mention);

        // Assert
        response.Should().Contain("The Magic Conch says...");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildMagicConchResponse_WithEmptyOrNullQuestion_ShouldStillBuildValidResponse(string question)
    {
        // Arrange
        var mention = "<@123456789>";

        // Act
        var response = MagicConchModule.BuildMagicConchResponse(question, mention);

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().StartWith(mention);
        response.Should().Contain("The Magic Conch says...");
    }

    [Fact]
    public void BuildMagicConchResponse_ShouldContainNewlineBetweenQuestionAndAnswer()
    {
        // Arrange
        var question = "Is this a test?";
        var mention = "<@123456789>";

        // Act
        var response = MagicConchModule.BuildMagicConchResponse(question, mention);

        // Assert
        var lines = response.Split(Environment.NewLine);
        lines.Should().HaveCount(2);
        lines[0].Should().Contain(mention).And.Contain(question);
        lines[1].Should().Contain("The Magic Conch says...");
    }
}