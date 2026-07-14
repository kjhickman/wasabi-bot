use poise::serenity_prelude as serenity;
use rand::{Rng, RngExt};

pub struct Data {
    pub pool: sqlx::PgPool,
}

pub type Error = anyhow::Error;
pub type Context<'a> = poise::Context<'a, Data, Error>;

pub fn all() -> Vec<poise::Command<Data, Error>> {
    vec![flip(), choose(), conch(), help(), stats(), mock()]
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

/// Flip a coin.
#[poise::command(slash_command)]
pub async fn flip(ctx: Context<'_>) -> Result<(), Error> {
    let result = flip_coin(&mut rand::rng());
    ctx.say(format!("The coin lands on... **{result}!**")).await?;
    Ok(())
}

pub fn flip_coin(rng: &mut impl Rng) -> &'static str {
    if rng.random_range(0..2) == 0 { "Heads" } else { "Tails" }
}

/// Choose randomly from 2-7 options.
#[poise::command(slash_command)]
#[allow(clippy::too_many_arguments)]
pub async fn choose(
    ctx: Context<'_>,
    #[description = "First option"] option1: String,
    #[description = "Second option"] option2: String,
    #[description = "Third option"] option3: Option<String>,
    #[description = "Fourth option"] option4: Option<String>,
    #[description = "Fifth option"] option5: Option<String>,
    #[description = "Sixth option"] option6: Option<String>,
    #[description = "Seventh option"] option7: Option<String>,
) -> Result<(), Error> {
    let options = distinct_options(&[
        Some(option1),
        Some(option2),
        option3,
        option4,
        option5,
        option6,
        option7,
    ]);

    if options.len() < 2 {
        return send_ephemeral(&ctx, "Please provide at least 2 distinct options.").await;
    }

    let chosen = &options[rand::rng().random_range(0..options.len())];
    let response = format!(
        "Options: {}\nAnd the choice is... **{chosen}**",
        options.join(", ")
    );
    ctx.say(response).await?;
    Ok(())
}

pub fn distinct_options(raw: &[Option<String>]) -> Vec<String> {
    let mut options: Vec<String> = Vec::new();
    for option in raw.iter().flatten() {
        let trimmed = option.trim();
        if trimmed.is_empty() {
            continue;
        }
        if !options
            .iter()
            .any(|o| o.to_lowercase() == trimmed.to_lowercase())
        {
            options.push(trimmed.to_string());
        }
    }
    options
}

/// Ask the magic conch a question.
#[poise::command(slash_command)]
pub async fn conch(
    ctx: Context<'_>,
    #[description = "A yes/no question"] question: String,
) -> Result<(), Error> {
    // ponytail: weighted-random only, LLM answer path returns post-MVP
    let answer = conch_response(&mut rand::rng());
    let name = display_name(&ctx).await;
    ctx.say(format!(
        "{name} asked: *{question}*\nThe Magic Conch says... {answer}"
    ))
    .await?;
    Ok(())
}

pub const CONCH_RESPONSES: [(&str, u32); 5] = [
    ("Yes", 44),
    ("No", 32),
    ("I don't think so", 12),
    ("Maybe", 9),
    ("Try asking again", 3),
];

pub fn conch_response(rng: &mut impl Rng) -> &'static str {
    let total: u32 = CONCH_RESPONSES.iter().map(|(_, w)| w).sum();
    let mut roll = rng.random_range(0..total);
    for (response, weight) in CONCH_RESPONSES {
        if roll < weight {
            return response;
        }
        roll -= weight;
    }
    unreachable!("weights exhausted")
}

/// Shows all available commands and helpful links.
#[poise::command(slash_command)]
pub async fn help(ctx: Context<'_>) -> Result<(), Error> {
    send_ephemeral(&ctx, HELP_MESSAGE).await
}

const HELP_MESSAGE: &str = "\
# Wasabi Bot Help

## Available Commands

### Fun
• `/conch` - Ask the magic conch a yes/no question
• `/choose` - Choose randomly from 2-7 options
• `/flip` - Flip a coin
• `Mock` - Right-click a message → Apps → Mock

### Utility
• `/stats` - Show bot usage statistics
• `/help` - Shows this help message

## Helpful Links
• [Website](<https://wasabibot.com>)
• [GitHub Repository](<https://github.com/kjhickman/wasabi-bot>)
";

/// Show bot usage statistics.
#[poise::command(slash_command)]
pub async fn stats(ctx: Context<'_>) -> Result<(), Error> {
    let stats = crate::db::get_stats(
        &ctx.data().pool,
        ctx.channel_id().get() as i64,
        ctx.id() as i64,
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
        message.push_str(&format!(
            "## Most Used Command\n• **`/{command}`** - {count} uses\n"
        ));
    }
    if let Some((user, count)) = &stats.top_user {
        message.push_str(&format!("## Top User\n• **{user}** - {count} commands\n"));
    }
    message
}

#[poise::command(context_menu_command = "Mock")]
pub async fn mock(ctx: Context<'_>, message: serenity::Message) -> Result<(), Error> {
    if message.content.trim().is_empty() {
        return send_ephemeral(&ctx, "That message has no text to mock.").await;
    }
    ctx.say(mockify(&message.content)).await?;
    Ok(())
}

pub fn mockify(message: &str) -> String {
    let mut uppercase = true;
    message
        .chars()
        .map(|c| {
            if !c.is_alphabetic() {
                return c;
            }
            let out = if uppercase {
                c.to_uppercase().next().unwrap_or(c)
            } else {
                c.to_lowercase().next().unwrap_or(c)
            };
            uppercase = !uppercase;
            out
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use rand::SeedableRng;
    use rand::rngs::StdRng;

    #[test]
    fn mockify_alternates_case_skipping_non_letters() {
        assert_eq!(mockify("hello world"), "HeLlO wOrLd");
        assert_eq!(mockify("a b-c"), "A b-C");
        assert_eq!(mockify(""), "");
    }

    #[test]
    fn distinct_options_trims_and_dedupes_case_insensitively() {
        let options = distinct_options(&[
            Some("  Pizza ".into()),
            Some("pizza".into()),
            Some("Tacos".into()),
            None,
            Some("   ".into()),
        ]);
        assert_eq!(options, vec!["Pizza", "Tacos"]);
    }

    #[test]
    fn conch_response_covers_full_weight_range() {
        let mut rng = StdRng::seed_from_u64(42);
        for _ in 0..1000 {
            let response = conch_response(&mut rng);
            assert!(CONCH_RESPONSES.iter().any(|(r, _)| *r == response));
        }
    }

    #[test]
    fn flip_coin_returns_heads_or_tails() {
        let mut rng = StdRng::seed_from_u64(7);
        for _ in 0..100 {
            let result = flip_coin(&mut rng);
            assert!(result == "Heads" || result == "Tails");
        }
    }

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
