FROM rust:1-slim-bookworm AS build
WORKDIR /src

COPY Cargo.toml Cargo.lock ./
COPY migrations ./migrations
COPY src ./src
RUN cargo build --release

FROM debian:bookworm-slim AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /src/target/release/wasabi-bot /app/wasabi-bot
COPY --from=build /src/target/release/migrate /app/migrate

EXPOSE 8080
ENTRYPOINT ["/app/wasabi-bot"]
