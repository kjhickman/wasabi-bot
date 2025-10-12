using Amazon.Runtime.Credentials;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WasabiBot.Api.Features.RemindMe.Services;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordInfrastructure();
builder.AddAIInfrastructure();
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IReminderService, ReminderService>();

builder.Services.AddHostedService<ReminderProcessor>();

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton(_ => DefaultAWSCredentialsIdentityResolver.GetCredentials());
    builder.Services.AddSingleton<DsqlTokenSource>();
    builder.Services.AddSingleton(sp =>
    {
        var tokenSource = sp.GetRequiredService<DsqlTokenSource>();

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = "dvthki5gk26y76agneiy6wcn64.dsql.us-east-1.on.aws",
            Database = "postgres",
            Username = "admin",
            SslMode = SslMode.Require,
        };

        var dsb = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);
        dsb.UsePasswordProvider(
            passwordProvider: _ => tokenSource.GetAdminToken(),
            passwordProviderAsync: null);
        return dsb.Build();
    });

    builder.Services.AddDbContext<WasabiBotContext>((sp, options) =>
    {
        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
        options.UseNpgsql(dataSource);
    });
}
else
{
    builder.Services.AddDbContext<WasabiBotContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
    });
}

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();

app.MapGet("/", () => "Hello, world!");

app.Run();
