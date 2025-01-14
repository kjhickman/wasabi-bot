namespace WasabiBot.Tests.Integration.Web.Endpoints;

public class InteractionEndpointTests : IClassFixture<WasabiBotApiFactory>
{
    private readonly HttpClient _httpClient;

    public InteractionEndpointTests(WasabiBotApiFactory appFactory)
    {
        _httpClient = appFactory.CreateClient();
    }

    [Fact(Skip = "Not implemented")]
    public async Task Test()
    {
        await _httpClient.PostAsync("/v1/interaction", new StringContent(""), TestContext.Current.CancellationToken);
    }
}