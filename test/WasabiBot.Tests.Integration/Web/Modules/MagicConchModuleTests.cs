// using System.Net;
// using Discord;
// using Shouldly;
// using WasabiBot.Tests.Integration.Builders;
//
// namespace WasabiBot.Tests.Integration.Web.Modules;
//
// public class MagicConchModuleTests : IClassFixture<WasabiBotApiFactory>
// {
//     private readonly HttpClient _httpClient;
//     private readonly string _privateKey;
//
//     public MagicConchModuleTests(WasabiBotApiFactory appFactory)
//     {
//         _httpClient = appFactory.CreateClient();
//         _privateKey = appFactory.PrivateKey;
//     }
//
//     [Fact]
//     public async Task MagicConchModule_AcknowledgesPing_WhenValidPing()
//     {
//         var client = new DiscordInteractionTestClient(_privateKey);
//
//         var data = new DiscordInteractionDataBuilder()
//             .WithTestValues()
//             .WithName("conch")
//             .WithStringDataOption("question", "Will I pass this test?")
//             .Build();
//
//         var jsonBody = new InteractionBuilder()
//             .WithTestValues()
//             .WithType(InteractionType.ApplicationCommand)
//             .WithData(data)
//             .BuildJson();
//         
//         var response = await client.SendInteractionAsync(_httpClient, jsonBody);
//         response.StatusCode.ShouldBe(HttpStatusCode.OK);
//     }
// }