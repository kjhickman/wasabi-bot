use std::fmt::Write as _;

use super::{Context, Error, send_ephemeral};

/// Shows all available commands and helpful links.
#[poise::command(slash_command)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn help(ctx: Context<'_>) -> Result<(), Error> {
    send_ephemeral(&ctx, HELP_MESSAGE).await
}

const HELP_MESSAGE: &str = "\
# Wasabi Bot Help

## Available Commands

### Fun
• `/conch` - Ask the magic conch a yes/no question
• `/caption` - Generate a funny caption for an image
• `/choose` - Choose randomly from 2-7 options
• `/flip` - Flip a coin
• `Mock` - Right-click a message → Apps → Mock

### Utility
• `/stats` - Show bot usage statistics
• `/help` - Shows this help message

### Music
• `/play` - Play a URL or search YouTube Music, YouTube, then SoundCloud
• `/queue` - Show the current queue
• `/nowplaying` - Show the current track
• `/pause` - Pause playback
• `/resume` - Resume playback
• `/skip` - Skip the current track
• `/stop` - Stop playback and clear the queue
• `/leave` - Leave the voice channel

## Helpful Links
• [Website](<https://wasabibot.com>)
• [GitHub Repository](<https://github.com/kjhickman/wasabi-bot>)
";

/// Show bot usage statistics.
#[poise::command(slash_command)]
#[tracing::instrument(name = "discord.command", skip(ctx), fields(command = %ctx.command().qualified_name, user_id = %ctx.author().id.get(), channel_id = %ctx.channel_id().get()))]
pub async fn stats(ctx: Context<'_>) -> Result<(), Error> {
    let stats = crate::db::get_stats(
        &ctx.data().pool,
        ctx.channel_id().get().cast_signed(),
        ctx.id().cast_signed(),
    )
    .await?;
    send_ephemeral(&ctx, build_stats_message(&stats)).await
}

pub fn build_stats_message(stats: &crate::db::Stats) -> String {
    let mut message = format!(
        "# 📊 Bot Statistics\n\n## Interaction Counts\n• **Total interactions:** {}\n• **This channel:** {}\n",
        stats.total, stats.channel
    );
    if let Some((command, count)) = &stats.most_used_command {
        writeln!(
            message,
            "## Most Used Command\n• **`/{command}`** - {count} uses"
        )
        .expect("writing to a String cannot fail");
    }
    if let Some((user, count)) = &stats.top_user {
        writeln!(message, "## Top User\n• **{user}** - {count} commands")
            .expect("writing to a String cannot fail");
    }
    message
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stats_message_includes_optional_sections() {
        let stats = crate::db::Stats {
            total: 12,
            channel: 3,
            most_used_command: Some(("flip".into(), 5)),
            top_user: Some(("kyle".into(), 8)),
        };
        let message = build_stats_message(&stats);
        assert!(message.contains("**Total interactions:** 12"));
        assert!(message.contains("**This channel:** 3"));
        assert!(message.contains("`/flip`"));
        assert!(message.contains("**kyle** - 8 commands"));

        let empty = build_stats_message(&crate::db::Stats::default());
        assert!(!empty.contains("Most Used Command"));
        assert!(!empty.contains("Top User"));
    }
}
