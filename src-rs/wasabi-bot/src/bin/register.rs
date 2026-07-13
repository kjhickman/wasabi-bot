use poise::serenity_prelude as serenity;
use wasabi_bot::commands;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let token = std::env::var("Discord__Token")
        .or_else(|_| std::env::var("DISCORD_TOKEN"))
        .map_err(|_| anyhow::anyhow!("neither Discord__Token nor DISCORD_TOKEN is set"))?;

    let http = serenity::Http::new(&token);
    let application = http.get_current_application_info().await?;
    http.set_application_id(application.id);

    poise::builtins::register_globally(&http, &commands::all()).await?;
    Ok(())
}
