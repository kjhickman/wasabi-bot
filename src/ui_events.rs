#[derive(Clone, Copy, Debug)]
pub enum UiEvent {
    VoiceStateChanged {
        guild_id: poise::serenity_prelude::GuildId,
        user_id: poise::serenity_prelude::UserId,
        is_bot: bool,
    },
}

pub type UiEvents = tokio::sync::broadcast::Sender<UiEvent>;
