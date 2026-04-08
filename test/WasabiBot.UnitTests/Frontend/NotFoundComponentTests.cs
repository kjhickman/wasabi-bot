using Bunit;
using WasabiBot.Api.Frontend.Pages;

namespace WasabiBot.UnitTests.Frontend;

public class NotFoundComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Test]
    public async Task Render_Shows404HeadingAndHomeLink()
    {
        var cut = _context.Render<NotFound>();

        await Assert.That(cut.Markup).Contains("404");
        await Assert.That(cut.Find("#not-found-heading").TextContent.Trim()).IsEqualTo("This page does not exist.");
        await Assert.That(cut.Find("a").GetAttribute("href")).IsEqualTo("/");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
