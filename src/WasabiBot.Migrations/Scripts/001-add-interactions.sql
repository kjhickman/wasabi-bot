create table interactions (
    id numeric(20, 0) not null primary key,
    channel_id numeric(20, 0) not null,
    application_id numeric(20, 0) not null,
    user_id numeric(20, 0) not null,
    guild_id numeric(20, 0) null,
    username text not null,
    global_name text null,
    nickname text null,
    data jsonb null,
    created_at timestamptz not null
);

create index ix_interactions_user_id on interactions(user_id);
create index ix_interactions_guild_id on interactions(guild_id) where guild_id is not null;
