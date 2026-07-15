use lavalink_rs::model::track::{TrackData, TrackInfo};
use lavalink_rs::prelude::*;
use poise::serenity_prelude as serenity;
use serenity::utils::MessageBuilder;
use tokio::sync::OwnedMutexGuard;

use super::{Context, Error, send_ephemeral};

/// Play a song.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx, input), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn play(
    ctx: Context<'_>,
    #[description = "URL or search query"]
    #[rest]
    input: String,
) -> Result<(), Error> {
    let Some(lavalink) = ctx.data().lavalink.clone() else {
        send_ephemeral(&ctx, "Music is not configured for this bot.").await?;
        return Ok(());
    };
    ctx.defer().await?;
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    if ensure_same_voice_channel(&ctx).await?.is_none() {
        return Ok(());
    }

    let loaded = match load_tracks(&lavalink, guild_id, &input).await? {
        Some(result) => result,
        None => {
            ctx.say("No playable tracks found.").await?;
            return Ok(());
        }
    };

    let _guard = guild_voice_lock(&ctx).await;
    let Some(channel_id) = ensure_same_voice_channel(&ctx).await? else {
        return Ok(());
    };
    let player = join_author_channel(&ctx, &lavalink, guild_id, channel_id).await?;
    let queue = player.get_queue();
    let queued_before = queue.get_count().await?;

    let (tracks, response) = match loaded {
        LoadedTracks::Single { source, track } => {
            let response = format!(
                "Queued {} at position {} from {}.",
                track_link(&track.info),
                queued_before + 1,
                source
            );
            (vec![track.into()], response)
        }
        LoadedTracks::Playlist { name, tracks } => {
            let response = format!(
                "Queued playlist **{}** ({} tracks).",
                safe_text(&name),
                tracks.len()
            );
            (tracks, response)
        }
    };

    queue.append(tracks.into())?;
    if player.get_player().await?.track.is_none() && queue.get_track(0).await?.is_some() {
        player.skip()?;
    }
    mark_voice_active(&ctx).await;

    ctx.say(response).await?;
    Ok(())
}

/// Skip the current track.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn skip(ctx: Context<'_>) -> Result<(), Error> {
    let _guard = guild_voice_lock(&ctx).await;
    let Some(player) = current_player(&ctx).await? else {
        return Ok(());
    };
    if ensure_same_voice_channel(&ctx).await?.is_none() {
        return Ok(());
    }

    if let Some(track) = player.get_player().await?.track {
        player.skip()?;
        mark_voice_active(&ctx).await;
        ctx.say(format!("Skipped {}.", safe_text(&track.info.title)))
            .await?;
    } else {
        ctx.say("Nothing is playing.").await?;
    }
    Ok(())
}

/// Stop playback and clear the queue.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn stop(ctx: Context<'_>) -> Result<(), Error> {
    let _guard = guild_voice_lock(&ctx).await;
    let Some(player) = current_player(&ctx).await? else {
        return Ok(());
    };
    if ensure_same_voice_channel(&ctx).await?.is_none() {
        return Ok(());
    }

    let queue = player.get_queue();
    let had_queue = queue.get_count().await? > 0;
    queue.clear()?;
    if player.get_player().await?.track.is_some() {
        player.stop_now().await?;
        mark_voice_idle(&ctx).await;
        ctx.say("Stopped playback and cleared the queue.").await?;
    } else if had_queue {
        mark_voice_idle(&ctx).await;
        ctx.say("Cleared the queue.").await?;
    } else {
        ctx.say("Nothing is playing.").await?;
    }
    Ok(())
}

/// Pause the current track.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn pause(ctx: Context<'_>) -> Result<(), Error> {
    set_pause(ctx, true).await
}

/// Resume playback.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn resume(ctx: Context<'_>) -> Result<(), Error> {
    set_pause(ctx, false).await
}

/// Show the current queue.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn queue(ctx: Context<'_>) -> Result<(), Error> {
    let Some(player) = current_player(&ctx).await? else {
        return Ok(());
    };

    let player_data = player.get_player().await?;
    let now = match player_data.track {
        Some(track) => format!("Now playing: {}", track_link(&track.info)),
        None => "Now playing: nothing".to_string(),
    };

    let tracks = player.get_queue().get_queue().await?;
    let total = tracks.len();
    let upcoming = tracks
        .iter()
        .take(10)
        .enumerate()
        .map(|(idx, track)| format!("{}. {}", idx + 1, track_link(&track.track.info)));

    let mut lines = vec![now];
    lines.extend(upcoming);
    if total > 10 {
        lines.push(format!("...and {} more.", total - 10));
    }
    ctx.say(lines.join("\n")).await?;
    Ok(())
}

/// Show the current track.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn nowplaying(ctx: Context<'_>) -> Result<(), Error> {
    let Some(player) = current_player(&ctx).await? else {
        return Ok(());
    };
    let player_data = player.get_player().await?;
    let queue_count = player.get_queue().get_count().await?;

    if let Some(track) = player_data.track {
        let suffix = if queue_count == 0 {
            String::new()
        } else {
            format!(" ({} queued)", queue_count)
        };
        ctx.say(format!(
            "Now playing: {}{}",
            track_link(&track.info),
            suffix
        ))
        .await?;
    } else {
        ctx.say("Nothing is playing.").await?;
    }
    Ok(())
}

/// Leave the voice channel.
#[poise::command(slash_command, guild_only)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn leave(ctx: Context<'_>) -> Result<(), Error> {
    let _guard = guild_voice_lock(&ctx).await;
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    if ensure_same_voice_channel(&ctx).await?.is_none() {
        return Ok(());
    }

    if let Some(lavalink) = &ctx.data().lavalink {
        let _ = lavalink.delete_player(lava_guild(guild_id)).await;
    }
    clear_voice_state(&ctx).await;
    let manager = songbird::get(ctx.serenity_context())
        .await
        .ok_or_else(|| anyhow::anyhow!("songbird is not registered"))?
        .clone();
    if manager.get(guild_id).is_some() {
        manager.remove(guild_id).await?;
        ctx.say("Left voice channel.").await?;
    } else {
        ctx.say("Nothing to leave.").await?;
    }
    Ok(())
}

async fn set_pause(ctx: Context<'_>, paused: bool) -> Result<(), Error> {
    let _guard = guild_voice_lock(&ctx).await;
    let Some(player) = current_player(&ctx).await? else {
        return Ok(());
    };
    if ensure_same_voice_channel(&ctx).await?.is_none() {
        return Ok(());
    }
    if player.get_player().await?.track.is_none() {
        ctx.say("Nothing is playing.").await?;
        return Ok(());
    }
    player.set_pause(paused).await?;
    mark_voice_paused(&ctx, paused).await;
    ctx.say(if paused { "Paused." } else { "Resumed." }).await?;
    Ok(())
}

async fn mark_voice_active(ctx: &Context<'_>) {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let mut voice_state = ctx.data().voice_state.lock().await;
    let state = voice_state.entry(guild_id).or_default();
    state.idle_since = None;
    state.paused_since = None;
}

async fn mark_voice_idle(ctx: &Context<'_>) {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let mut voice_state = ctx.data().voice_state.lock().await;
    let state = voice_state.entry(guild_id).or_default();
    state.idle_since = Some(std::time::Instant::now());
    state.paused_since = None;
}

async fn mark_voice_paused(ctx: &Context<'_>, paused: bool) {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let mut voice_state = ctx.data().voice_state.lock().await;
    let state = voice_state.entry(guild_id).or_default();
    state.paused_since = paused.then(std::time::Instant::now);
}

async fn clear_voice_state(ctx: &Context<'_>) {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    ctx.data().voice_state.lock().await.remove(&guild_id);
}

async fn guild_voice_lock(ctx: &Context<'_>) -> OwnedMutexGuard<()> {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let lock = {
        let mut locks = ctx.data().voice_locks.lock().await;
        locks
            .entry(guild_id)
            .or_insert_with(|| std::sync::Arc::new(tokio::sync::Mutex::new(())))
            .clone()
    };
    lock.lock_owned().await
}

async fn current_player(ctx: &Context<'_>) -> Result<Option<PlayerContext>, Error> {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let Some(lavalink) = &ctx.data().lavalink else {
        send_ephemeral(ctx, "Music is not configured for this bot.").await?;
        return Ok(None);
    };
    let Some(player) = lavalink.get_player_context(lava_guild(guild_id)) else {
        ctx.say("Nothing is playing.").await?;
        return Ok(None);
    };
    Ok(Some(player))
}

async fn join_author_channel(
    ctx: &Context<'_>,
    lavalink: &LavalinkClient,
    guild_id: serenity::GuildId,
    channel_id: serenity::ChannelId,
) -> Result<PlayerContext, Error> {
    if let Some(player) = lavalink.get_player_context(lava_guild(guild_id)) {
        return Ok(player);
    }

    let manager = songbird::get(ctx.serenity_context())
        .await
        .ok_or_else(|| anyhow::anyhow!("songbird is not registered"))?
        .clone();
    let (connection_info, _) = manager.join_gateway(guild_id, channel_id).await?;

    let player = lavalink
        .create_player_context(lava_guild(guild_id), connection_info)
        .await?;
    Ok(player)
}

async fn ensure_same_voice_channel(
    ctx: &Context<'_>,
) -> Result<Option<serenity::ChannelId>, Error> {
    let Some(author_channel) = author_voice_channel(ctx) else {
        ctx.say("Join a voice channel first.").await?;
        return Ok(None);
    };
    let bot_channel = bot_voice_channel(ctx);
    if let Some(bot) = bot_channel
        && author_channel != bot
    {
        ctx.say("Join my voice channel first.").await?;
        return Ok(None);
    }
    Ok(Some(author_channel))
}

fn author_voice_channel(ctx: &Context<'_>) -> Option<serenity::ChannelId> {
    ctx.guild()?
        .voice_states
        .get(&ctx.author().id)
        .and_then(|state| state.channel_id)
}

fn bot_voice_channel(ctx: &Context<'_>) -> Option<serenity::ChannelId> {
    ctx.guild()?
        .voice_states
        .get(&ctx.serenity_context().cache.current_user().id)
        .and_then(|state| state.channel_id)
}

async fn load_tracks(
    lavalink: &LavalinkClient,
    guild_id: serenity::GuildId,
    input: &str,
) -> Result<Option<LoadedTracks>, Error> {
    for (source, query) in track_queries(input)? {
        match lavalink.load_tracks(lava_guild(guild_id), &query).await {
            Ok(loaded) => match loaded.data {
                Some(TrackLoadData::Track(track)) => {
                    return Ok(Some(LoadedTracks::Single { source, track }));
                }
                Some(TrackLoadData::Search(tracks)) => {
                    if let Some(track) = tracks.into_iter().next() {
                        return Ok(Some(LoadedTracks::Single { source, track }));
                    }
                }
                Some(TrackLoadData::Playlist(playlist)) => {
                    let tracks: Vec<_> = playlist.tracks.into_iter().map(Into::into).collect();
                    if !tracks.is_empty() {
                        return Ok(Some(LoadedTracks::Playlist {
                            name: playlist.info.name,
                            tracks,
                        }));
                    }
                }
                Some(TrackLoadData::Error(error)) => tracing::warn!(
                    message = %error.message,
                    cause = %error.cause,
                    query = %query,
                    "Lavalink track load error"
                ),
                None => {}
            },
            Err(error) => tracing::warn!(?error, query = %query, "failed to load tracks"),
        }
    }
    Ok(None)
}

enum LoadedTracks {
    Single {
        source: &'static str,
        track: TrackData,
    },
    Playlist {
        name: String,
        tracks: Vec<TrackInQueue>,
    },
}

fn track_queries(input: &str) -> Result<Vec<(&'static str, String)>, Error> {
    let input = input.trim();
    if input.starts_with("http://") || input.starts_with("https://") {
        Ok(vec![("URL", input.to_string())])
    } else {
        Ok(vec![
            (
                "YouTube Music",
                SearchEngines::YouTubeMusic.to_query(input)?,
            ),
            ("YouTube", SearchEngines::YouTube.to_query(input)?),
            ("SoundCloud", SearchEngines::SoundCloud.to_query(input)?),
        ])
    }
}

fn lava_guild(guild_id: serenity::GuildId) -> lavalink_rs::model::GuildId {
    guild_id.get().into()
}

fn track_link(info: &TrackInfo) -> String {
    let label = MessageBuilder::new()
        .push_safe(&info.author)
        .push(" - ")
        .push_safe(&info.title)
        .build();
    match info.uri.as_deref() {
        Some(uri) => format!("[{label}](<{uri}>)"),
        None => label,
    }
}

fn safe_text(value: &str) -> String {
    MessageBuilder::new().push_safe(value).build()
}

#[cfg(test)]
mod tests {
    use super::track_queries;

    #[test]
    fn track_queries_passes_urls_through() {
        assert_eq!(
            track_queries("https://example.com/song").unwrap(),
            vec![("URL", "https://example.com/song".to_string())]
        );
    }

    #[test]
    fn track_queries_prefer_youtube_music_then_youtube_then_soundcloud() {
        assert_eq!(
            track_queries("big tune").unwrap(),
            vec![
                ("YouTube Music", "ytmsearch:big tune".to_string()),
                ("YouTube", "ytsearch:big tune".to_string()),
                ("SoundCloud", "scsearch:big tune".to_string()),
            ]
        );
    }
}
