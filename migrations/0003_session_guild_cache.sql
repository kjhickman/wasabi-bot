ALTER TABLE web_sessions
    ADD COLUMN guilds jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN guilds_fetched_at timestamp with time zone NOT NULL DEFAULT now(),
    ADD COLUMN guilds_refresh_attempted_at timestamp with time zone NOT NULL DEFAULT to_timestamp(0);

UPDATE web_sessions SET guilds_fetched_at = to_timestamp(0);

CREATE INDEX web_sessions_guild_refresh_idx
    ON web_sessions (guilds_refresh_attempted_at, guilds_fetched_at);
