use lavalink_rs::{model::track::TrackInfo, prelude::PlayerContext};
use poise::serenity_prelude as serenity;
use serenity::utils::MessageBuilder;
use tokio::sync::OwnedMutexGuard;

use super::{Context, Error, send_ephemeral};
use crate::{music, voice};

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

    let Some(loaded) = music::load_tracks(&lavalink, guild_id, &input).await? else {
        send_ephemeral(&ctx, "No playable tracks found.").await?;
        return Ok(());
    };

    let _guard = guild_voice_lock(&ctx).await;
    let Some(channel_id) = ensure_same_voice_channel(&ctx).await? else {
        return Ok(());
    };
    let player = music::join(
        ctx.serenity_context(),
        &lavalink,
        &ctx.data().voice_state,
        guild_id,
        channel_id,
    )
    .await?;
    let response = match music::enqueue(&player, &ctx.data().voice_state, guild_id, loaded).await? {
        music::EnqueueOutcome::Track {
            source,
            info,
            position,
        } => format!(
            "Queued {} at position {} from {}.",
            track_link(&info),
            position,
            source
        ),
        music::EnqueueOutcome::Playlist { name, count } => {
            format!(
                "Queued playlist **{}** ({} tracks).",
                safe_text(&name),
                count
            )
        }
    };

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

    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    if let Some(info) = music::skip(&player, &ctx.data().voice_state, guild_id).await? {
        ctx.say(format!("Skipped {}.", safe_text(&info.title)))
            .await?;
    } else {
        send_ephemeral(&ctx, "Nothing is playing.").await?;
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

    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    match music::stop(&player, &ctx.data().voice_state, guild_id).await? {
        music::StopOutcome::PlaybackStopped => {
            ctx.say("Stopped playback and cleared the queue.").await?;
        }
        music::StopOutcome::QueueCleared => {
            ctx.say("Cleared the queue.").await?;
        }
        music::StopOutcome::Nothing => {
            send_ephemeral(&ctx, "Nothing is playing.").await?;
        }
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

    let snapshot = music::snapshot(&player).await?;
    let now = match snapshot.current {
        Some(track) => format!("Now playing: {}", track_link(&track.info)),
        None => "Now playing: nothing".to_string(),
    };

    let total = snapshot.upcoming.len();
    let upcoming = snapshot
        .upcoming
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
    let (current, queue_count) = music::now_playing(&player).await?;

    if let Some(track) = current {
        let suffix = if queue_count == 0 {
            String::new()
        } else {
            format!(" ({queue_count} queued)")
        };
        ctx.say(format!(
            "Now playing: {}{}",
            track_link(&track.info),
            suffix
        ))
        .await?;
    } else {
        send_ephemeral(&ctx, "Nothing is playing.").await?;
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

    if music::leave(
        ctx.serenity_context(),
        ctx.data().lavalink.as_ref(),
        &ctx.data().voice_state,
        guild_id,
    )
    .await?
    {
        ctx.say("Left voice channel.").await?;
    } else {
        send_ephemeral(&ctx, "Nothing to leave.").await?;
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
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    if !music::set_paused(&player, &ctx.data().voice_state, guild_id, paused).await? {
        send_ephemeral(&ctx, "Nothing is playing.").await?;
        return Ok(());
    }
    ctx.say(if paused { "Paused." } else { "Resumed." }).await?;
    Ok(())
}

async fn guild_voice_lock(ctx: &Context<'_>) -> OwnedMutexGuard<()> {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    voice::lock_guild(&ctx.data().voice_locks, guild_id).await
}

async fn current_player(ctx: &Context<'_>) -> Result<Option<PlayerContext>, Error> {
    let guild_id = ctx.guild_id().expect("guild_only command has a guild_id");
    let Some(lavalink) = &ctx.data().lavalink else {
        send_ephemeral(ctx, "Music is not configured for this bot.").await?;
        return Ok(None);
    };
    let Some(player) = music::player(lavalink, guild_id) else {
        send_ephemeral(ctx, "Nothing is playing.").await?;
        return Ok(None);
    };
    Ok(Some(player))
}

async fn ensure_same_voice_channel(
    ctx: &Context<'_>,
) -> Result<Option<serenity::ChannelId>, Error> {
    let Some(author_channel) = author_voice_channel(ctx) else {
        send_ephemeral(ctx, "Join a voice channel first.").await?;
        return Ok(None);
    };
    let bot_channel = bot_voice_channel(ctx);
    if let Some(bot) = bot_channel
        && author_channel != bot
    {
        send_ephemeral(ctx, "Join my voice channel first.").await?;
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
    voice::bot_channel(
        ctx.serenity_context().cache.as_ref(),
        ctx.guild_id().expect("guild_only command has a guild_id"),
    )
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
