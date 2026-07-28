CREATE TABLE web_sessions (
    id_hash bytea PRIMARY KEY,
    user_id bigint NOT NULL CHECK (user_id > 0),
    username text NOT NULL,
    global_name text,
    discriminator text NOT NULL,
    avatar text,
    access_token bytea NOT NULL,
    refresh_token bytea NOT NULL,
    token_expires_at timestamp with time zone NOT NULL,
    csrf_hash bytea NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX web_sessions_expires_at_idx ON web_sessions (expires_at);

CREATE TABLE oauth_states (
    id_hash bytea PRIMARY KEY,
    expires_at timestamp with time zone NOT NULL
);
