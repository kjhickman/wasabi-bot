create table interactions (
    id bigint not null primary key,
    channel_id bigint not null,
    application_id bigint not null,
    user_id bigint not null,
    guild_id bigint null,
    username text not null,
    global_name text null,
    nickname text null,
    data jsonb null,
    created_at timestamptz not null
);

create index ix_interactions_user_id on interactions(user_id);
create index ix_interactions_guild_id on interactions(guild_id) where guild_id is not null;
