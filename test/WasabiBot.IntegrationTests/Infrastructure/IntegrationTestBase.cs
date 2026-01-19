using TUnit.Core;
using WasabiBot.DataAccess;

namespace WasabiBot.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for all integration tests.
/// Provides access to test database and Respawn resets.
/// </summary>
public abstract class IntegrationTestBase
{
    private PostgresTestFixture? _fixture;

    protected PostgresTestFixture Fixture => _fixture ?? throw new InvalidOperationException("Fixture not initialized");

    /// <summary>Initializes the test fixture and resets the database before each test.</summary>
    [Before(HookType.Test)]
    public async Task BeforeEachTest()
    {
        _fixture = new PostgresTestFixture();
        await _fixture.InitializeAsync();
        await _fixture.ResetDatabaseAsync();
    }

    /// <summary>Cleans up the test fixture after each test.</summary>
    [After(HookType.Test)]
    public async Task AfterEachTest()
    {
        if (_fixture != null)
        {
            await _fixture.DisposeAsync();
        }
    }

    /// <summary>Creates a new context for this test.</summary>
    protected WasabiBotContext CreateContext() => Fixture.CreateContext();

    /// <summary>Resets the database state for the next test.</summary>
    protected async Task ResetDatabaseAsync() => await Fixture.ResetDatabaseAsync();
}
