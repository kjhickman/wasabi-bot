using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Interactions;

public class GetInteractionsTests
{
    private static string BuildCursor(DateTimeOffset createdAt, long id)
    {
        var cursorData = $"{createdAt:O}|{id}";
        var bytes = Encoding.UTF8.GetBytes(cursorData);
        return Convert.ToBase64String(bytes);
    }

    [Test]
    public async Task Handle_WithValidRequest_ReturnsOkWithInteractions()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interactions = new[] { InteractionEntityBuilder.Create().Build() };
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        await mockService.Received(1).GetAllAsync(Arg.Any<GetAllInteractionsRequest>());
    }

    [Test]
    public async Task Handle_WithMissingUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .AsUnauthenticatedUser()
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
        await mockService.DidNotReceive().GetAllAsync(Arg.Any<GetAllInteractionsRequest>());
    }

    [Test]
    public async Task Handle_WithEmptyUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }

    [Test]
    public async Task Handle_WithNonNumericUserId_ReturnsUnauthorized()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("not-a-number")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<UnauthorizedHttpResult>();
    }

    [Test]
    public async Task Handle_WithLimitZero_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 0, null, null, mockService);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
        await mockService.DidNotReceive().GetAllAsync(Arg.Any<GetAllInteractionsRequest>());
    }

    [Test]
    public async Task Handle_WithNegativeLimit_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: -5, null, null, mockService);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithLimitAbove100_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 101, null, null, mockService);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithValidLimit_UsesSpecifiedLimit()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, limit: 50, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r => r.Limit == 50));
    }

    [Test]
    public async Task Handle_WithoutLimit_UsesDefaultLimit100()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r => r.Limit == 100));
    }

    [Test]
    public async Task Handle_WithInvalidCursor_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, "invalid-cursor", mockService);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithMalformedBase64Cursor_ReturnsBadRequest()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, "not!valid!base64!", mockService);

        // Assert
        await Assert.That(result.GetType().Name).Contains("BadRequest");
    }

    [Test]
    public async Task Handle_WithValidCursor_ParsesAndUsesIt()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var cursorId = 12345L;
        var cursor = BuildCursor(createdAt, cursorId);
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, null, null, cursor, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.Cursor != null &&
            r.Cursor.Id == cursorId));
    }

    [Test]
    public async Task Handle_WithoutSort_UsesDescendingByDefault()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.SortDirection == SortDirection.Desc));
    }

    [Test]
    public async Task Handle_WithAscendingSort_UsesAscending()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, null, SortDirection.Asc, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.SortDirection == SortDirection.Asc));
    }

    [Test]
    public async Task Handle_WithChannelId_PassesChannelIdToService()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var channelId = 999888777L;
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, channelId, null, null, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.ChannelId == channelId));
    }

    [Test]
    public async Task Handle_WithApplicationId_PassesApplicationIdToService()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var applicationId = 555444333L;
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, applicationId, null, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.ApplicationId == applicationId));
    }

    [Test]
    public async Task Handle_WithGuildId_PassesGuildIdToService()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var guildId = 111222333L;
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, guildId, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.GuildId == guildId));
    }

    [Test]
    public async Task Handle_WithMoreResultsThanLimit_SetsHasMoreTrue()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interactions = Enumerable.Range(1, 11)
            .Select(i => InteractionEntityBuilder.Create().WithId(i).Build())
            .ToArray();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 10, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.HasMore).IsTrue();
        await Assert.That(response.NextCursor).IsNotNull();
    }

    [Test]
    public async Task Handle_WithFewerResultsThanLimit_SetsHasMoreFalse()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interactions = Enumerable.Range(1, 5)
            .Select(i => InteractionEntityBuilder.Create().WithId(i).Build())
            .ToArray();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 10, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.HasMore).IsFalse();
        await Assert.That(response.NextCursor).IsNull();
    }

    [Test]
    public async Task Handle_WithExactlyLimitResults_SetsHasMoreFalse()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interactions = Enumerable.Range(1, 10)
            .Select(i => InteractionEntityBuilder.Create().WithId(i).Build())
            .ToArray();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 10, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.HasMore).IsFalse();
        await Assert.That(response.NextCursor).IsNull();
    }

    [Test]
    public async Task Handle_WithMoreResults_ReturnsOnlyLimitedResults()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interactions = Enumerable.Range(1, 15)
            .Select(i => InteractionEntityBuilder.Create().WithId(i).Build())
            .ToArray();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 10, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.Interactions.Count()).IsEqualTo(10);
    }

    [Test]
    public async Task Handle_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.Interactions.Count()).IsEqualTo(0);
        await Assert.That(response.HasMore).IsFalse();
        await Assert.That(response.NextCursor).IsNull();
    }

    [Test]
    public async Task Handle_WithMoreResults_NextCursorUsesLastReturnedItem()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var now = DateTimeOffset.UtcNow;
        var interactions = Enumerable.Range(1, 11)
            .Select(i => InteractionEntityBuilder.Create().WithId(i).WithCreatedAt(now.AddMinutes(-i)).Build())
            .ToArray();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns(interactions);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, limit: 10, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;

        await Assert.That(response.NextCursor).IsNotNull();
        await Assert.That(response.NextCursor).IsNotEmpty();

        // Verify cursor is base64 encoded
        var cursorBytes = Convert.FromBase64String(response.NextCursor!);
        var cursorString = Encoding.UTF8.GetString(cursorBytes);
        await Assert.That(cursorString).Contains("|");
    }

    [Test]
    public async Task Handle_PassesUserIdToService()
    {
        // Arrange
        var userId = "987654321";
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId(userId)
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.UserId == long.Parse(userId)));
    }

    [Test]
    public async Task Handle_WithAllFilters_PassesAllToService()
    {
        // Arrange
        var userId = "123456789";
        var channelId = 111L;
        var applicationId = 222L;
        var guildId = 333L;
        var limit = 50;
        var sort = SortDirection.Asc;
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId(userId)
            .Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([]);

        // Act
        await GetInteractions.Handle(
            user, channelId, applicationId, guildId, limit, sort, null, mockService);

        // Assert
        await mockService.Received(1).GetAllAsync(Arg.Is<GetAllInteractionsRequest>(r =>
            r.UserId == long.Parse(userId) &&
            r.ChannelId == channelId &&
            r.ApplicationId == applicationId &&
            r.GuildId == guildId &&
            r.Limit == limit &&
            r.SortDirection == sort));
    }

    [Test]
    public async Task Handle_ReturnsInteractionsAsDtos()
    {
        // Arrange
        var user = ClaimsPrincipalBuilder.Create()
            .WithUserId("123456789")
            .Build();
        var interaction = InteractionEntityBuilder.Create().WithId(42).WithUsername("testuser").Build();
        var mockService = Substitute.For<IInteractionService>();
        mockService.GetAllAsync(Arg.Any<GetAllInteractionsRequest>())
            .Returns([interaction]);

        // Act
        var result = await GetInteractions.Handle(
            user, null, null, null, null, null, null, mockService);

        // Assert
        await Assert.That(result).IsTypeOf<Ok<GetInteractionsResponse>>();
        var okResult = (Ok<GetInteractionsResponse>)result;
        var response = okResult.Value!;
        var dto = response.Interactions.First();

        await Assert.That(dto.Id).IsEqualTo(42);
        await Assert.That(dto.Username).IsEqualTo("testuser");
    }
}
