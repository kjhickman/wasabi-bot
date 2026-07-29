use std::{collections::HashMap, sync::Arc, time::Instant};

use poise::serenity_prelude as serenity;
use tokio::sync::{Mutex, OwnedMutexGuard};

pub type VoiceLocks = Arc<Mutex<HashMap<serenity::GuildId, Arc<Mutex<()>>>>>;
pub type VoiceStates = Arc<Mutex<HashMap<serenity::GuildId, VoiceState>>>;

#[derive(Default)]
pub struct VoiceState {
    pub idle_since: Option<Instant>,
    pub paused_since: Option<Instant>,
}

pub async fn lock_guild(
    voice_locks: &VoiceLocks,
    guild_id: serenity::GuildId,
) -> OwnedMutexGuard<()> {
    let lock = {
        let mut locks = voice_locks.lock().await;
        locks
            .entry(guild_id)
            .or_insert_with(|| Arc::new(Mutex::new(())))
            .clone()
    };
    lock.lock_owned().await
}

pub async fn mark_active(voice_states: &VoiceStates, guild_id: serenity::GuildId) {
    let mut voice_states = voice_states.lock().await;
    let state = voice_states.entry(guild_id).or_default();
    state.idle_since = None;
    state.paused_since = None;
    drop(voice_states);
}

pub async fn mark_idle(voice_states: &VoiceStates, guild_id: serenity::GuildId) {
    let mut voice_states = voice_states.lock().await;
    let state = voice_states.entry(guild_id).or_default();
    state.idle_since = Some(Instant::now());
    state.paused_since = None;
    drop(voice_states);
}

pub async fn mark_paused(voice_states: &VoiceStates, guild_id: serenity::GuildId, paused: bool) {
    let mut voice_states = voice_states.lock().await;
    let state = voice_states.entry(guild_id).or_default();
    state.paused_since = paused.then(Instant::now);
    drop(voice_states);
}

pub async fn clear_state(voice_states: &VoiceStates, guild_id: serenity::GuildId) {
    voice_states.lock().await.remove(&guild_id);
}

#[must_use]
pub fn bot_channel(
    cache: &serenity::Cache,
    guild_id: serenity::GuildId,
) -> Option<serenity::ChannelId> {
    cache
        .guild(guild_id)?
        .voice_states
        .get(&cache.current_user().id)
        .and_then(|state| state.channel_id)
}
