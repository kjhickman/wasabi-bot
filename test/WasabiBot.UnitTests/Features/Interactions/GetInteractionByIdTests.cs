using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Entities;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Interactions;

public class GetInteractionByIdTests
{
    [Test]
    public async Task Handle_WithValidId_ReturnsOkWithInteraction()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create().WithId(42).Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(42, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        await mockService.Received(1).GetByIdAsync(42);
    }

    [Test]
    public async Task Handle_WithValidId_ReturnsCorrectDto()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create()
            .WithId(42)
            .WithUserId(123456789)
            .WithChannelId(999)
            .WithApplicationId(888)
            .WithUsername("testuser")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(42, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        var okResult = (Ok<InteractionDto>)result;
        var dto = okResult.Value!;

        await Assert.That(dto.Id).IsEqualTo(42);
        await Assert.That(dto.UserId).IsEqualTo(123456789);
        await Assert.That(dto.ChannelId).IsEqualTo(999);
        await Assert.That(dto.ApplicationId).IsEqualTo(888);
        await Assert.That(dto.Username).IsEqualTo("testuser");
    }

    [Test]
    public async Task Handle_WithZeroId_ReturnsBadRequest()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractionById.Handle(0, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<BadRequest<string>>();
        await mockService.DidNotReceive().GetByIdAsync(Arg.Any<long>());
    }

    [Test]
    public async Task Handle_WithNegativeId_ReturnsBadRequest()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractionById.Handle(-1, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<BadRequest<string>>();
        await mockService.DidNotReceive().GetByIdAsync(Arg.Any<long>());
    }

    [Test]
    public async Task Handle_WithNegativeLargeId_ReturnsBadRequest()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractionById.Handle(-999999, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<BadRequest<string>>();
    }

    [Test]
    public async Task Handle_WithInvalidId_ReturnsCorrectErrorMessage()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractionById.Handle(0, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<BadRequest<string>>();
        var badRequestResult = (BadRequest<string>)result;
        await Assert.That(badRequestResult.Value).IsEqualTo("Invalid interaction ID.");
    }

    [Test]
    public async Task Handle_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(999).Returns((InteractionEntity?)null);

        // Act
        var result = await GetInteractionById.Handle(999, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<NotFound>();
        await mockService.Received(1).GetByIdAsync(999);
    }

    [Test]
    public async Task Handle_WithValidPositiveId_CallsServiceWithCorrectId()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create().WithId(12345).Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(12345).Returns(interaction);

        // Act
        await GetInteractionById.Handle(12345, mockService);

        // Assert
        await mockService.Received(1).GetByIdAsync(12345);
    }

    [Test]
    public async Task Handle_WithLargeValidId_ReturnsOk()
    {
        // Arrange
        var largeId = 999_999_999_999L;
        var interaction = InteractionEntityBuilder.Create().WithId(largeId).Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(largeId).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(largeId, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        var okResult = (Ok<InteractionDto>)result;
        await Assert.That(okResult.Value!.Id).IsEqualTo(largeId);
    }

    [Test]
    public async Task Handle_MapsEntityToDtoCorrectly()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create()
            .WithId(42)
            .WithUserId(123456789)
            .WithChannelId(999888777)
            .WithApplicationId(555444333)
            .WithGuildId(111222333)
            .WithUsername("testuser")
            .WithGlobalName("Test User")
            .WithNickname("Tester")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(42, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        var okResult = (Ok<InteractionDto>)result;
        var dto = okResult.Value!;

        await Assert.That(dto.Id).IsEqualTo(interaction.Id);
        await Assert.That(dto.UserId).IsEqualTo(interaction.UserId);
        await Assert.That(dto.ChannelId).IsEqualTo(interaction.ChannelId);
        await Assert.That(dto.ApplicationId).IsEqualTo(interaction.ApplicationId);
        await Assert.That(dto.GuildId).IsEqualTo(interaction.GuildId);
        await Assert.That(dto.Username).IsEqualTo(interaction.Username);
        await Assert.That(dto.GlobalName).IsEqualTo(interaction.GlobalName);
        await Assert.That(dto.Nickname).IsEqualTo(interaction.Nickname);
        await Assert.That(dto.CreatedAt).IsEqualTo(interaction.CreatedAt);
    }

    [Test]
    public async Task Handle_WithNullGuildId_MapsDtoCorrectly()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create().WithId(42).WithGuildId(null).Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(42, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        var okResult = (Ok<InteractionDto>)result;
        await Assert.That(okResult.Value!.GuildId).IsNull();
    }

    [Test]
    public async Task Handle_WithNullOptionalFields_MapsDtoCorrectly()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create()
            .WithId(42)
            .WithGlobalName(null)
            .WithNickname(null)
            .WithData(null)
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        var result = await GetInteractionById.Handle(42, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<InteractionDto>>();
        var okResult = (Ok<InteractionDto>)result;
        var dto = okResult.Value!;

        await Assert.That(dto.GlobalName).IsNull();
        await Assert.That(dto.Nickname).IsNull();
        await Assert.That(dto.Data).IsNull();
    }

    [Test]
    public async Task Handle_ServiceReturnsNull_ReturnsNotFound()
    {
        // Arrange
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(Arg.Any<long>()).Returns((InteractionEntity?)null);

        // Act
        var result = await GetInteractionById.Handle(12345, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<NotFound>();
    }

    [Test]
    public async Task Handle_OnlyCallsServiceOnce()
    {
        // Arrange
        var interaction = InteractionEntityBuilder.Create().WithId(42).Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetByIdAsync(42).Returns(interaction);

        // Act
        await GetInteractionById.Handle(42, mockService);

        // Assert
        await mockService.Received(1).GetByIdAsync(Arg.Any<long>());
    }
}
