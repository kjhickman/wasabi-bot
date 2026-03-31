using Bunit;
using WasabiBot.Api.Components.Pages;

namespace WasabiBot.UnitTests.Components;

public class TokenGeneratorComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_ShowsGenerateAndCopyButton()
    {
        var cut = _context.Render<TokenGenerator>();

        await Assert.That(cut.Find("#generate-token-button").TextContent.Trim()).IsEqualTo("Generate and copy token");
        await Assert.That(cut.Find("#generate-token-button").GetAttribute("data-token-endpoint")).IsEqualTo("/api/v1/token");
        await Assert.That(cut.Find("#generate-token-button").GetAttribute("data-tooltip")).IsEqualTo("Generate and copy");
    }

    [Test]
    public async Task Render_DoesNotRenderTokenOrStatusText()
    {
        var cut = _context.Render<TokenGenerator>();

        await Assert.That(cut.FindAll("#token-output").Count).IsEqualTo(0);
        await Assert.That(cut.FindAll("#token-feedback").Count).IsEqualTo(0);
    }

    [Test]
    public async Task Render_ShowsApiDocsLink()
    {
        var cut = _context.Render<TokenGenerator>();

        await Assert.That(cut.Find("#docs-link").GetAttribute("href")).IsEqualTo("/scalar/v1");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
