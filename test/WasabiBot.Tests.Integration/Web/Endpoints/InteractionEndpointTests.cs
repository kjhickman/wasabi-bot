using System.Net;
using Discord;
using Shouldly;
using WasabiBot.Tests.Integration.Builders;

namespace WasabiBot.Tests.Integration.Web.Endpoints;

[Collection(nameof(WasabiBotApiFactory))]
public class InteractionEndpointTests
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

    [Fact]
    public async Task InteractionEndpoint_ReturnsBadRequest_WhenInvalidSignature()
    {
        var jsonBody = new InteractionBuilder()
            .WithTestValues()
            .WithType(InteractionType.Ping)
            .BuildJson();
        
        var response = await _httpClient.PostAsync("/v1/interaction", new StringContent(jsonBody), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}