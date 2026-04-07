using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.UnitTests.Components;

internal static class ComponentTestHelpers
{
    public static ApiTokenFactory CreateTokenFactory(TimeSpan? lifetime = null)
    {
        var key = new byte[32];
        Random.Shared.NextBytes(key);
        var signingKey = new SymmetricSecurityKey(key);
        var options = new ApiTokenOptions(signingKey, lifetime ?? TimeSpan.FromHours(1));
        return new ApiTokenFactory(options);
    }

    public static IRenderedComponent<TComponent> RenderWithAuthentication<TComponent>(
        this BunitContext context,
        AuthenticationState authState)
        where TComponent : IComponent
    {
        return context.RenderWithAuthentication<TComponent>(authState, _ => { });
    }

    public static IRenderedComponent<TComponent> RenderWithAuthentication<TComponent>(
        this BunitContext context,
        AuthenticationState authState,
        Action<ComponentParameterCollectionBuilder<TComponent>> buildParameters)
        where TComponent : IComponent
    {
        var wrapper = context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(parameter => parameter.Value, Task.FromResult(authState))
            .AddChildContent<TComponent>(buildParameters));

        return wrapper.FindComponent<TComponent>();
    }
}
