use lavalink_rs::{
    model::track::TrackData,
    prelude::{LavalinkClient, SearchEngines, TrackInQueue, TrackLoadData},
};
use poise::serenity_prelude as serenity;

use super::lava_guild;

pub enum LoadedTracks {
    Single {
        source: &'static str,
        track: Box<TrackData>,
    },
    Playlist {
        name: String,
        tracks: Vec<TrackInQueue>,
    },
}

pub(super) async fn load(
    lavalink: &LavalinkClient,
    guild_id: serenity::GuildId,
    input: &str,
) -> anyhow::Result<Option<LoadedTracks>> {
    for (source, query) in track_queries(input)? {
        match lavalink.load_tracks(lava_guild(guild_id), &query).await {
            Ok(loaded) => match loaded.data {
                Some(TrackLoadData::Track(track)) => {
                    return Ok(Some(LoadedTracks::Single {
                        source,
                        track: Box::new(track),
                    }));
                }
                Some(TrackLoadData::Search(tracks)) => {
                    if let Some(track) = tracks.into_iter().next() {
                        return Ok(Some(LoadedTracks::Single {
                            source,
                            track: Box::new(track),
                        }));
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

fn track_queries(input: &str) -> anyhow::Result<Vec<(&'static str, String)>> {
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
