use poise::serenity_prelude as serenity;

use crate::{
    commands::{Data, Error},
    music,
    voice::{self, VoiceLocks, VoiceState, VoiceStates},
};

const IDLE_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(30);
const PAUSED_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(60);
const MAINTENANCE_INTERVAL: std::time::Duration = std::time::Duration::from_secs(5);

pub(super) fn start(
    ctx: serenity::Context,
    lavalink: Option<lavalink_rs::prelude::LavalinkClient>,
    voice_locks: VoiceLocks,
    voice_state: VoiceStates,
) {
    tokio::spawn(async move {
        let mut interval = tokio::time::interval(MAINTENANCE_INTERVAL);
        loop {
            interval.tick().await;
            if let Err(error) = maintain(&ctx, lavalink.as_ref(), &voice_locks, &voice_state).await
            {
                tracing::warn!(?error, "voice maintenance failed");
            }
        }
    });
}

async fn maintain(
    ctx: &serenity::Context,
    lavalink: Option<&lavalink_rs::prelude::LavalinkClient>,
    voice_locks: &VoiceLocks,
    voice_state: &VoiceStates,
) -> Result<(), Error> {
    let Some(lavalink) = lavalink else {
        return Ok(());
    };
    let guild_ids: Vec<_> = {
        let voice_state = voice_state.lock().await;
        voice_state.keys().copied().collect()
    };

    for guild_id in guild_ids {
        let _guard = voice::lock_guild(voice_locks, guild_id).await;
        if voice::bot_channel(ctx.cache.as_ref(), guild_id).is_none() {
            if let Err(error) = music::leave(ctx, Some(lavalink), voice_state, guild_id).await {
                tracing::warn!(?error, %guild_id, "failed to clean up voice connection");
            }
            continue;
        }
        let Some(player) = music::player(lavalink, guild_id) else {
            if let Err(error) = music::leave(ctx, Some(lavalink), voice_state, guild_id).await {
                tracing::warn!(?error, %guild_id, "failed to clean up voice connection");
            }
            continue;
        };

        let player_data = match player.get_player().await {
            Ok(player_data) => player_data,
            Err(error) => {
                tracing::warn!(?error, %guild_id, "failed to inspect voice player");
                continue;
            }
        };
        let queue_empty = match player.get_queue().get_count().await {
            Ok(count) => count == 0,
            Err(error) => {
                tracing::warn!(?error, %guild_id, "failed to inspect voice queue");
                continue;
            }
        };
        let idle = player_data.track.is_none() && queue_empty;
        let now = std::time::Instant::now();
        let should_disconnect = {
            let mut voice_state = voice_state.lock().await;
            let state = voice_state.entry(guild_id).or_default();
            let expired = update_timeout_state(state, idle, now);
            drop(voice_state);
            expired
        };
        if should_disconnect
            && let Err(error) = music::leave(ctx, Some(lavalink), voice_state, guild_id).await
        {
            tracing::warn!(?error, %guild_id, "failed to disconnect inactive voice connection");
        }
    }
    Ok(())
}

fn update_timeout_state(state: &mut VoiceState, idle: bool, now: std::time::Instant) -> bool {
    if idle {
        state.idle_since.get_or_insert(now);
    } else {
        state.idle_since = None;
    }
    let idle_expired = state
        .idle_since
        .is_some_and(|since| now.duration_since(since) >= IDLE_TIMEOUT);
    let paused_expired = state
        .paused_since
        .is_some_and(|since| now.duration_since(since) >= PAUSED_TIMEOUT);
    idle_expired || paused_expired
}

pub(super) async fn disconnect_if_alone(
    ctx: &serenity::Context,
    data: &Data,
    guild_id: serenity::GuildId,
) -> Result<(), Error> {
    let Some(bot_channel_id) = voice::bot_channel(ctx.cache.as_ref(), guild_id) else {
        return Ok(());
    };
    if has_human_in_channel(&ctx.cache, guild_id, bot_channel_id) {
        return Ok(());
    }
    let _guard = voice::lock_guild(&data.voice_locks, guild_id).await;
    let Some(bot_channel_id) = voice::bot_channel(ctx.cache.as_ref(), guild_id) else {
        return Ok(());
    };
    if !has_human_in_channel(&ctx.cache, guild_id, bot_channel_id) {
        if let Some(lavalink) = &data.lavalink {
            music::leave(ctx, Some(lavalink), &data.voice_state, guild_id).await?;
        } else {
            music::leave(ctx, None, &data.voice_state, guild_id).await?;
        }
    }
    Ok(())
}

fn has_human_in_channel(
    cache: &impl AsRef<serenity::Cache>,
    guild_id: serenity::GuildId,
    channel_id: serenity::ChannelId,
) -> bool {
    let cache = cache.as_ref();
    let current_user_id = cache.current_user().id;
    cache.guild(guild_id).is_some_and(|guild| {
        guild.voice_states.values().any(|state| {
            state.channel_id == Some(channel_id)
                && state.user_id != current_user_id
                && !state
                    .member
                    .as_ref()
                    .or_else(|| guild.members.get(&state.user_id))
                    .is_some_and(|member| member.user.bot)
        })
    })
}

#[cfg(test)]
mod tests {
    use std::time::{Duration, Instant};

    use crate::voice::VoiceState;

    use super::{IDLE_TIMEOUT, PAUSED_TIMEOUT, update_timeout_state};

    #[test]
    fn timeout_state_tracks_idle_and_paused_expiration() {
        let now = Instant::now();
        let mut state = VoiceState::default();
        let before_idle_timeout = IDLE_TIMEOUT
            .checked_sub(Duration::from_millis(1))
            .expect("idle timeout exceeds one millisecond");
        let before_paused_timeout = PAUSED_TIMEOUT
            .checked_sub(Duration::from_millis(1))
            .expect("paused timeout exceeds one millisecond");

        assert!(!update_timeout_state(&mut state, true, now));
        assert_eq!(state.idle_since, Some(now));
        assert!(!update_timeout_state(
            &mut state,
            true,
            now + before_idle_timeout
        ));
        assert!(update_timeout_state(&mut state, true, now + IDLE_TIMEOUT));

        assert!(!update_timeout_state(&mut state, false, now + IDLE_TIMEOUT));
        assert_eq!(state.idle_since, None);

        state.paused_since = Some(now);
        assert!(!update_timeout_state(
            &mut state,
            false,
            now + before_paused_timeout
        ));
        assert!(update_timeout_state(
            &mut state,
            false,
            now + PAUSED_TIMEOUT
        ));
    }
}
