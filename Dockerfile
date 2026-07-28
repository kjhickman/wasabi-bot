FROM lukemathwalker/cargo-chef:latest-rust-1-slim-bookworm AS chef
WORKDIR /src

FROM chef AS planner
COPY . .
RUN cargo chef prepare --recipe-path recipe.json

FROM chef AS build
COPY --from=planner /src/recipe.json recipe.json
RUN cargo chef cook --release --recipe-path recipe.json
RUN cargo install topcoat-cli --version 0.5.0 --locked
COPY build.rs styles.css ./
COPY migrations ./migrations
COPY src ./src
RUN cargo build --release \
    && topcoat asset bundle --release --bin wasabi-bot

FROM debian:bookworm-slim AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /src/target/release/wasabi-bot /app/wasabi-bot
COPY --from=build /src/target/release/migrate /app/migrate
COPY --from=build /src/target/assets /app/assets

EXPOSE 8080
ENTRYPOINT ["/app/wasabi-bot"]
