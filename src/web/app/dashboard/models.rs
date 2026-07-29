pub struct DashboardView<'a> {
    pub playback: PlaybackView<'a>,
    pub queue_summary: &'a str,
    pub queue: Vec<QueueItemView<'a>>,
    pub recommendations: Vec<SearchResultView<'a>>,
}

pub struct PlaybackView<'a> {
    pub guild_name: &'a str,
    pub channel_name: &'a str,
    pub show: &'a str,
    pub title: &'a str,
    pub artist: &'a str,
    pub progress: f32,
    pub elapsed: &'a str,
    pub duration: &'a str,
    pub artwork_label: &'a str,
    pub artwork_code: &'a str,
    pub volume: &'a str,
}

pub struct QueueItemView<'a> {
    pub position: &'a str,
    pub title: &'a str,
    pub artist: &'a str,
    pub duration: &'a str,
    pub up_next: bool,
}

pub struct SearchResultView<'a> {
    pub title: &'a str,
    pub artist: &'a str,
    pub duration: &'a str,
}

impl<'a> DashboardView<'a> {
    pub fn fixture(guild_name: &'a str, channel_name: &'a str) -> Self {
        Self {
            playback: PlaybackView {
                guild_name,
                channel_name,
                show: "After Hours Radio",
                title: "Green Static",
                artist: "Night Market",
                progress: 43.0,
                elapsed: "1:42",
                duration: "3:58",
                artwork_label: "Abstract green album artwork for Green Static",
                artwork_code: "NM—042",
                volume: "67%",
            },
            queue_summary: "4 tracks · 14 minutes",
            queue: vec![
                QueueItemView {
                    position: "01",
                    title: "Chrome Garden",
                    artist: "Signal Path",
                    duration: "3:44",
                    up_next: true,
                },
                QueueItemView {
                    position: "02",
                    title: "Low Battery",
                    artist: "Night Shift",
                    duration: "2:51",
                    up_next: false,
                },
                QueueItemView {
                    position: "03",
                    title: "Window Seat",
                    artist: "City Sleep",
                    duration: "4:18",
                    up_next: false,
                },
                QueueItemView {
                    position: "04",
                    title: "Neon Receipt",
                    artist: "Corner Store",
                    duration: "3:12",
                    up_next: false,
                },
            ],
            recommendations: vec![
                SearchResultView {
                    title: "Soft Circuit",
                    artist: "Public Memory",
                    duration: "3:21",
                },
                SearchResultView {
                    title: "Limewire Nights",
                    artist: "Modem Club",
                    duration: "4:05",
                },
                SearchResultView {
                    title: "Last Train Home",
                    artist: "City Sleep",
                    duration: "2:56",
                },
            ],
        }
    }
}
