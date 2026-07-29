use poise::serenity_prelude as serenity;

use crate::db;

pub(super) fn to_record(command: &serenity::CommandInteraction) -> db::InteractionRecord {
    db::InteractionRecord {
        id: command.id.get().cast_signed(),
        channel_id: command.channel_id.get().cast_signed(),
        application_id: command.application_id.get().cast_signed(),
        user_id: command.user.id.get().cast_signed(),
        guild_id: command.guild_id.map(|id| id.get().cast_signed()),
        username: command.user.name.clone(),
        global_name: command.user.global_name.clone(),
        nickname: command.member.as_ref().and_then(|m| m.nick.clone()),
        data: serde_json::to_value(&command.data).ok(),
        created_at: snowflake_timestamp(command.id.get()),
    }
}

fn snowflake_timestamp(id: u64) -> time::OffsetDateTime {
    const DISCORD_EPOCH_MS: i128 = 1_420_070_400_000;
    let ms = i128::from(id >> 22) + DISCORD_EPOCH_MS;
    time::OffsetDateTime::from_unix_timestamp_nanos(ms * 1_000_000)
        .unwrap_or(time::OffsetDateTime::UNIX_EPOCH)
}

#[cfg(test)]
mod tests {
    use super::snowflake_timestamp;

    #[test]
    fn snowflake_timestamp_decodes_discord_epoch() {
        let ts = snowflake_timestamp(175_928_847_299_117_063);
        assert_eq!(ts.unix_timestamp(), 1_462_015_105);
    }
}
