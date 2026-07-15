mod fun;
mod music;
mod utility;

pub use fun::{caption, choose, conch, flip, mock};
pub use music::{leave, nowplaying, pause, play, queue, resume, skip, stop};
pub use utility::{help, stats};

pub struct Data {
    pub pool: sqlx::PgPool,
    pub http: reqwest::Client,
    pub gemini_api_key: Option<String>,
    pub lavalink: Option<lavalink_rs::prelude::LavalinkClient>,
}

pub type Error = anyhow::Error;
pub type Context<'a> = poise::Context<'a, Data, Error>;
pub(crate) type PlayerChannel = (
    poise::serenity_prelude::model::id::ChannelId,
    std::sync::Arc<poise::serenity_prelude::Http>,
);

pub fn all() -> Vec<poise::Command<Data, Error>> {
    vec![
        flip(),
        choose(),
        conch(),
        caption(),
        help(),
        stats(),
        mock(),
        play(),
        skip(),
        stop(),
        pause(),
        resume(),
        queue(),
        nowplaying(),
        leave(),
    ]
}

async fn display_name(ctx: &Context<'_>) -> String {
    if let Some(member) = ctx.author_member().await {
        return member.display_name().to_string();
    }
    ctx.author().display_name().to_string()
}

async fn send_ephemeral(ctx: &Context<'_>, content: impl Into<String>) -> Result<(), Error> {
    ctx.send(
        poise::CreateReply::default()
            .content(content.into())
            .ephemeral(true),
    )
    .await?;
    Ok(())
}
