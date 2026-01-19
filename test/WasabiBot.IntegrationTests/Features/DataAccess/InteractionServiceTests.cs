using OpenTelemetry.Trace;
using TUnit.Core;
using WasabiBot.DataAccess.Services;
using WasabiBot.IntegrationTests.Infrastructure;
using WasabiBot.TestShared.Builders;

namespace WasabiBot.IntegrationTests.Features.DataAccess;

/// <summary>
/// Integration tests for InteractionService.
/// Tests CRUD operations and pagination logic against a real PostgreSQL database.
/// </summary>
public class InteractionServiceTests : IntegrationTestBase
{
    [Test]
    public async Task CreateAsync_ShouldInsertInteractionIntoDatabase()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interaction = InteractionEntityBuilder.Create(id: 123, userId: 456);

        // Act
        var result = await service.CreateAsync(interaction);

        // Assert
        await Assert.That(result).IsTrue();
        await using var assertContext = CreateContext();
        await TestAssertions.AssertInteractionExistsAsync(assertContext, 123);
    }

    [Test]
    public async Task GetByIdAsync_ShouldRetrieveSingleInteractionById()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interaction = InteractionEntityBuilder.Create(id: 789, username: "TestUser123");

        await service.CreateAsync(interaction);

        // Act
        var result = await service.GetByIdAsync(789);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(789L);
        await Assert.That(result.Username).IsEqualTo("TestUser123");
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNullForNonExistentId()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));

        // Act
        var result = await service.GetByIdAsync(99999);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAllAsync_ShouldRetrieveAllInteractionsWithoutFilters()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = InteractionEntityBuilder.CreateMultiple(count: 5);

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { Limit = 100 };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(5);
    }

    [Test]
    public async Task GetAllAsync_ShouldFilterByUserId()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, userId: 111),
            InteractionEntityBuilder.Create(id: 2, userId: 111),
            InteractionEntityBuilder.Create(id: 3, userId: 222),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { UserId = 111, Limit = 100 };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);
        foreach (var result in results)
        {
            await Assert.That(result.UserId).IsEqualTo(111);
        }
    }

    [Test]
    public async Task GetAllAsync_ShouldFilterByChannelId()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, channelId: 100),
            InteractionEntityBuilder.Create(id: 2, channelId: 100),
            InteractionEntityBuilder.Create(id: 3, channelId: 200),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { ChannelId = 100, Limit = 100 };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);
        foreach (var result in results)
        {
            await Assert.That(result.ChannelId).IsEqualTo(100);
        }
    }

    [Test]
    public async Task GetAllAsync_ShouldFilterByApplicationId()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, applicationId: 300),
            InteractionEntityBuilder.Create(id: 2, applicationId: 300),
            InteractionEntityBuilder.Create(id: 3, applicationId: 400),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { ApplicationId = 300, Limit = 100 };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);
        foreach (var result in results)
        {
            await Assert.That(result.ApplicationId).IsEqualTo(300);
        }
    }

    [Test]
    public async Task GetAllAsync_ShouldFilterByGuildId()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, guildId: 500),
            InteractionEntityBuilder.Create(id: 2, guildId: 500),
            InteractionEntityBuilder.Create(id: 3, guildId: null),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { GuildId = 500, Limit = 100 };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(2);
        foreach (var result in results)
        {
            await Assert.That(result.GuildId).IsEqualTo(500);
        }
    }

    [Test]
    public async Task GetAllAsync_ShouldRespectLimitParameter()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = InteractionEntityBuilder.CreateMultiple(count: 10);

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { Limit = 3 };
        var results = await service.GetAllAsync(request);

        // Assert
        // Service retrieves Limit + 1 to detect if more results exist
        await Assert.That(results.Length).IsGreaterThanOrEqualTo(3);
        await Assert.That(results.Length).IsLessThanOrEqualTo(4);
    }

    [Test]
    public async Task GetAllAsync_ShouldSortByCreatedAtDescendingByDefault()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var baseTime = DateTimeOffset.UtcNow;
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, createdAt: baseTime.AddDays(-2)),
            InteractionEntityBuilder.Create(id: 2, createdAt: baseTime.AddDays(-1)),
            InteractionEntityBuilder.Create(id: 3, createdAt: baseTime),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { Limit = 100, SortDirection = SortDirection.Desc };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results[0].Id).IsEqualTo(3L); // Most recent first
        await Assert.That(results[1].Id).IsEqualTo(2L);
        await Assert.That(results[2].Id).IsEqualTo(1L); // Oldest last
    }

    [Test]
    public async Task GetAllAsync_ShouldSortByCreatedAtAscending()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var baseTime = DateTimeOffset.UtcNow;
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, createdAt: baseTime.AddDays(-2)),
            InteractionEntityBuilder.Create(id: 2, createdAt: baseTime.AddDays(-1)),
            InteractionEntityBuilder.Create(id: 3, createdAt: baseTime),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act
        var request = new GetAllInteractionsRequest { Limit = 100, SortDirection = SortDirection.Asc };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results[0].Id).IsEqualTo(1L); // Oldest first
        await Assert.That(results[1].Id).IsEqualTo(2L);
        await Assert.That(results[2].Id).IsEqualTo(3L); // Most recent last
    }

    [Test]
    public async Task GetAllAsync_ShouldPaginateUsingCursor()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var baseTime = DateTimeOffset.UtcNow;
        var interactions = InteractionEntityBuilder.CreateMultiple(count: 5);

        // Reset timestamps to be predictable for cursor-based pagination
        for (int i = 0; i < interactions.Length; i++)
        {
            interactions[i].CreatedAt = baseTime.AddDays(i);
        }

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act - First page
        var firstPageRequest = new GetAllInteractionsRequest { Limit = 2, SortDirection = SortDirection.Desc };
        var firstPage = await service.GetAllAsync(firstPageRequest);

        // Act - Second page using cursor
        var secondPageRequest = new GetAllInteractionsRequest
        {
            Limit = 2,
            SortDirection = SortDirection.Desc,
            Cursor = new InteractionCursor
            {
                CreatedAt = firstPage[firstPage.Length - 1].CreatedAt,
                Id = firstPage[firstPage.Length - 1].Id
            }
        };
        var secondPage = await service.GetAllAsync(secondPageRequest);

        // Assert
        await Assert.That(firstPage.Length).IsGreaterThan(0);
        await Assert.That(secondPage.Length).IsGreaterThan(0);
        // Ensure no overlap between pages
        await Assert.That(firstPage.Select(x => x.Id)).DoesNotContain(secondPage[0].Id);
    }

    [Test]
    public async Task GetAllAsync_ShouldCombineMultipleFilters()
    {
        // Arrange
        await using var context = CreateContext();
        var service = new InteractionService(context, TracerProvider.Default.GetTracer("test"));
        var interactions = new[]
        {
            InteractionEntityBuilder.Create(id: 1, userId: 111, channelId: 222, applicationId: 333),
            InteractionEntityBuilder.Create(id: 2, userId: 111, channelId: 222, applicationId: 999),
            InteractionEntityBuilder.Create(id: 3, userId: 111, channelId: 555, applicationId: 333),
            InteractionEntityBuilder.Create(id: 4, userId: 999, channelId: 222, applicationId: 333),
        };

        foreach (var interaction in interactions)
        {
            await service.CreateAsync(interaction);
        }

        // Act - Filter by userId, channelId, and applicationId
        var request = new GetAllInteractionsRequest
        {
            UserId = 111,
            ChannelId = 222,
            ApplicationId = 333,
            Limit = 100
        };
        var results = await service.GetAllAsync(request);

        // Assert
        await Assert.That(results.Length).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(1L);
    }
}
