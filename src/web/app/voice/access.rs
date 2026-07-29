use poise::serenity_prelude as serenity;

use crate::web::{BotState, auth::DiscordGuild};

#[derive(Clone, Debug)]
pub(in crate::web::app) struct ChannelAccess {
    guild_id: serenity::GuildId,
    pub(in crate::web::app) guild_name: String,
    channel_id: serenity::ChannelId,
    pub(in crate::web::app) channel_name: String,
    pub(in crate::web::app) bot_channel_name: Option<String>,
}

pub(in crate::web::app) enum Access {
    LoggedOut,
    NoSharedGuild,
    NotInVoice,
    Unavailable,
    Joinable(ChannelAccess),
    Conflict(ChannelAccess),
    Ready(ChannelAccess),
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) struct JoinTarget {
    pub(super) guild_id: serenity::GuildId,
    pub(super) channel_id: serenity::ChannelId,
}

pub(super) fn join_target(
    bot: &BotState,
    user_id: u64,
    guilds: &[DiscordGuild],
) -> Option<JoinTarget> {
    match resolve_access(bot, user_id, guilds) {
        Access::Joinable(channel) => Some(JoinTarget {
            guild_id: channel.guild_id,
            channel_id: channel.channel_id,
        }),
        _ => None,
    }
}

pub(super) fn ready_target(
    bot: &BotState,
    user_id: u64,
    guilds: &[DiscordGuild],
) -> Option<JoinTarget> {
    match resolve_access(bot, user_id, guilds) {
        Access::Ready(channel) => Some(JoinTarget {
            guild_id: channel.guild_id,
            channel_id: channel.channel_id,
        }),
        _ => None,
    }
}

pub(in crate::web::app) fn resolve_access(
    bot: &BotState,
    user_id: u64,
    guilds: &[DiscordGuild],
) -> Access {
    let user_id = serenity::UserId::new(user_id);
    let bot_id = bot.serenity.cache.current_user().id;

    for guild_id in bot.serenity.cache.guilds() {
        let Some(guild) = bot.serenity.cache.guild(guild_id) else {
            continue;
        };
        let Some(channel_id) = guild
            .voice_states
            .get(&user_id)
            .and_then(|voice| voice.channel_id)
        else {
            continue;
        };
        let bot_channel_id = guild
            .voice_states
            .get(&bot_id)
            .and_then(|voice| voice.channel_id);
        let channel_name = guild.channels.get(&channel_id).map_or_else(
            || "Voice channel".to_owned(),
            |channel| channel.name.clone(),
        );
        let bot_channel_name = bot_channel_id
            .and_then(|id| guild.channels.get(&id).map(|channel| channel.name.clone()));
        let channel = ChannelAccess {
            guild_id,
            guild_name: guild.name.clone(),
            channel_id,
            channel_name,
            bot_channel_name,
        };
        return match voice_decision(Some(channel_id), bot_channel_id) {
            VoiceDecision::Joinable => Access::Joinable(channel),
            VoiceDecision::Ready => Access::Ready(channel),
            VoiceDecision::Conflict => Access::Conflict(channel),
            VoiceDecision::NotInVoice => unreachable!(),
        };
    }

    let shares_guild = guilds.iter().any(|guild| {
        guild
            .id
            .parse::<u64>()
            .is_ok_and(|guild_id| bot.serenity.cache.guild(guild_id).is_some())
    });
    if shares_guild {
        Access::NotInVoice
    } else {
        Access::NoSharedGuild
    }
}

#[derive(Debug, PartialEq, Eq)]
enum VoiceDecision {
    NotInVoice,
    Joinable,
    Conflict,
    Ready,
}

fn voice_decision(
    user_channel: Option<serenity::ChannelId>,
    bot_channel: Option<serenity::ChannelId>,
) -> VoiceDecision {
    match (user_channel, bot_channel) {
        (None, _) => VoiceDecision::NotInVoice,
        (Some(_), None) => VoiceDecision::Joinable,
        (Some(user), Some(bot)) if user == bot => VoiceDecision::Ready,
        (Some(_), Some(_)) => VoiceDecision::Conflict,
    }
}

#[cfg(test)]
mod tests {
    use poise::serenity_prelude::ChannelId;

    use super::{VoiceDecision, voice_decision};

    #[test]
    fn voice_access_requires_the_same_channel() {
        let lounge = ChannelId::new(1);
        let kitchen = ChannelId::new(2);

        assert_eq!(voice_decision(None, None), VoiceDecision::NotInVoice);
        assert_eq!(voice_decision(Some(lounge), None), VoiceDecision::Joinable);
        assert_eq!(
            voice_decision(Some(lounge), Some(kitchen)),
            VoiceDecision::Conflict
        );
        assert_eq!(
            voice_decision(Some(lounge), Some(lounge)),
            VoiceDecision::Ready
        );
    }
}
