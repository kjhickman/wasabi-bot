mod snapshot;
mod tracks;

use std::collections::VecDeque;

use lavalink_rs::{
    model::track::{TrackData, TrackInfo},
    prelude::{LavalinkClient, PlayerContext, TrackInQueue},
};
use poise::serenity_prelude as serenity;

use crate::voice::{self, VoiceStates};

pub use snapshot::{get as snapshot, now_playing};
pub use tracks::LoadedTracks;

pub struct Snapshot {
    pub current: Option<TrackData>,
    pub upcoming: VecDeque<TrackInQueue>,
}

pub enum EnqueueOutcome {
    Track {
        source: &'static str,
        info: TrackInfo,
        position: usize,
    },
    Playlist {
        name: String,
        count: usize,
    },
}

pub enum StopOutcome {
    Nothing,
    QueueCleared,
    PlaybackStopped,
}

pub async fn load_tracks(
    lavalink: &LavalinkClient,
    guild_id: serenity::GuildId,
    input: &str,
) -> anyhow::Result<Option<LoadedTracks>> {
    tracks::load(lavalink, guild_id, input).await
}

pub fn player(lavalink: &LavalinkClient, guild_id: serenity::GuildId) -> Option<PlayerContext> {
    lavalink.get_player_context(lava_guild(guild_id))
}

pub async fn join(
    ctx: &serenity::Context,
    lavalink: &LavalinkClient,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
    channel_id: serenity::ChannelId,
) -> anyhow::Result<PlayerContext> {
    let manager = songbird::get(ctx)
        .await
        .ok_or_else(|| anyhow::anyhow!("songbird is not registered"))?
        .clone();
    if let Some(player) = player(lavalink, guild_id) {
        if manager.get(guild_id).is_some() {
            voice::mark_idle(voice_states, guild_id).await;
            return Ok(player);
        }
        let _ = lavalink.delete_player(lava_guild(guild_id)).await;
    }
    let (connection_info, _) = manager.join_gateway(guild_id, channel_id).await?;
    let player = lavalink
        .create_player_context(lava_guild(guild_id), connection_info)
        .await?;
    voice::mark_idle(voice_states, guild_id).await;
    Ok(player)
}

pub async fn leave(
    ctx: &serenity::Context,
    lavalink: Option<&LavalinkClient>,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
) -> anyhow::Result<bool> {
    if let Some(lavalink) = lavalink
        && player(lavalink, guild_id).is_some()
        && let Err(error) = lavalink.delete_player(lava_guild(guild_id)).await
    {
        tracing::warn!(?error, %guild_id, "failed to delete Lavalink player");
    }
    let manager = songbird::get(ctx)
        .await
        .ok_or_else(|| anyhow::anyhow!("songbird is not registered"))?;
    let connected = manager.get(guild_id).is_some();
    if connected {
        manager.remove(guild_id).await?;
    }
    voice::clear_state(voice_states, guild_id).await;
    Ok(connected)
}

pub async fn enqueue(
    player: &PlayerContext,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
    loaded: LoadedTracks,
) -> anyhow::Result<EnqueueOutcome> {
    let queue = player.get_queue();
    let queued_before = queue.get_count().await?;
    let (tracks, outcome) = match loaded {
        LoadedTracks::Single { source, track } => {
            let outcome = EnqueueOutcome::Track {
                source,
                info: track.info.clone(),
                position: queued_before + 1,
            };
            (vec![(*track).into()], outcome)
        }
        LoadedTracks::Playlist { name, tracks } => {
            let outcome = EnqueueOutcome::Playlist {
                name,
                count: tracks.len(),
            };
            (tracks, outcome)
        }
    };
    queue.append(tracks.into())?;
    if player.get_player().await?.track.is_none() && queue.get_track(0).await?.is_some() {
        player.skip()?;
    }
    voice::mark_active(voice_states, guild_id).await;
    Ok(outcome)
}

pub async fn skip(
    player: &PlayerContext,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
) -> anyhow::Result<Option<TrackInfo>> {
    let Some(track) = player.get_player().await?.track else {
        return Ok(None);
    };
    player.skip()?;
    voice::mark_active(voice_states, guild_id).await;
    Ok(Some(track.info))
}

pub async fn stop(
    player: &PlayerContext,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
) -> anyhow::Result<StopOutcome> {
    let queue = player.get_queue();
    let had_queue = queue.get_count().await? > 0;
    queue.clear()?;
    let outcome = if player.get_player().await?.track.is_some() {
        player.stop_now().await?;
        StopOutcome::PlaybackStopped
    } else if had_queue {
        StopOutcome::QueueCleared
    } else {
        StopOutcome::Nothing
    };
    if !matches!(outcome, StopOutcome::Nothing) {
        voice::mark_idle(voice_states, guild_id).await;
    }
    Ok(outcome)
}

pub async fn set_paused(
    player: &PlayerContext,
    voice_states: &VoiceStates,
    guild_id: serenity::GuildId,
    paused: bool,
) -> anyhow::Result<bool> {
    if player.get_player().await?.track.is_none() {
        return Ok(false);
    }
    player.set_pause(paused).await?;
    voice::mark_paused(voice_states, guild_id, paused).await;
    Ok(true)
}

fn lava_guild(guild_id: serenity::GuildId) -> lavalink_rs::model::GuildId {
    guild_id.get().into()
}
