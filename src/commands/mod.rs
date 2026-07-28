mod fun;
pub(crate) mod music;
mod utility;

pub use fun::{caption, choose, conch, flip, mock};
pub use music::{leave, nowplaying, pause, play, queue, resume, skip, stop};
pub use utility::{help, stats};

pub(crate) type VoiceLocks = std::sync::Arc<
    tokio::sync::Mutex<
        std::collections::HashMap<
            poise::serenity_prelude::GuildId,
            std::sync::Arc<tokio::sync::Mutex<()>>,
        >,
    >,
>;

pub(crate) type VoiceStates = std::sync::Arc<
    tokio::sync::Mutex<std::collections::HashMap<poise::serenity_prelude::GuildId, VoiceState>>,
>;

pub(crate) async fn lock_guild_voice(
    voice_locks: &VoiceLocks,
    guild_id: poise::serenity_prelude::GuildId,
) -> tokio::sync::OwnedMutexGuard<()> {
    let lock = {
        let mut locks = voice_locks.lock().await;
        locks
            .entry(guild_id)
            .or_insert_with(|| std::sync::Arc::new(tokio::sync::Mutex::new(())))
            .clone()
    };
    lock.lock_owned().await
}

pub struct Data {
    pub pool: sqlx::PgPool,
    pub http: reqwest::Client,
    pub gemini_api_key: Option<String>,
    pub lavalink: Option<lavalink_rs::prelude::LavalinkClient>,
    pub voice_locks: VoiceLocks,
    pub voice_state: VoiceStates,
    pub ui_events: crate::web::UiEvents,
}

#[derive(Default)]
pub struct VoiceState {
    pub idle_since: Option<std::time::Instant>,
    pub paused_since: Option<std::time::Instant>,
}

pub type Error = anyhow::Error;
pub type Context<'a> = poise::Context<'a, Data, Error>;

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
