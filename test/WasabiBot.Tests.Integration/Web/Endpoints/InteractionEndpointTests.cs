using System.Net;
using Discord;
using Shouldly;
using WasabiBot.Tests.Integration.Builders;

namespace WasabiBot.Tests.Integration.Web.Endpoints;

public class InteractionEndpointTests : IClassFixture<WasabiBotApiFactory>
{
    private readonly HttpClient _httpClient;
    private readonly string _privateKey;

    public InteractionEndpointTests(WasabiBotApiFactory appFactory)
    {
        _httpClient = appFactory.CreateClient();
        _privateKey = appFactory.PrivateKey;
    }

    [Fact]
    public async Task InteractionEndpoint_AcknowledgesPing_WhenValidPing()
    {
        var client = new DiscordInteractionTestClient(_privateKey);
        var jsonBody = new InteractionBuilder()
          .WithTestValues()
          .WithType(InteractionType.Ping)
          .BuildJson();
        
        var response = await client.SendInteractionAsync(_httpClient, jsonBody);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}