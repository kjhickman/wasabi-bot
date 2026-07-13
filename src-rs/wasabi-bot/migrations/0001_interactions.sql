CREATE TABLE IF NOT EXISTS interactions (
    id bigint NOT NULL,
    channel_id bigint NOT NULL,
    application_id bigint NOT NULL,
    user_id bigint NOT NULL,
    guild_id bigint,
    username text NOT NULL,
    global_name text,
    nickname text,
    data jsonb,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_interactions PRIMARY KEY (id)
);
