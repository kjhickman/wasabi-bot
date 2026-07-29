use lavalink_rs::prelude::PlayerContext;

use super::Snapshot;

pub async fn get(player: &PlayerContext) -> anyhow::Result<Snapshot> {
    Ok(Snapshot {
        current: player.get_player().await?.track,
        upcoming: player.get_queue().get_queue().await?,
    })
}

pub async fn now_playing(
    player: &PlayerContext,
) -> anyhow::Result<(Option<lavalink_rs::model::track::TrackData>, usize)> {
    Ok((
        player.get_player().await?.track,
        player.get_queue().get_count().await?,
    ))
}
