pub fn router() -> axum::Router {
    axum::Router::new()
        .route(
            "/",
            axum::routing::get(|| async { "Wasabi Bot is under construction." }),
        )
        .route("/health", axum::routing::get(|| async { "ok" }))
        .route("/alive", axum::routing::get(|| async { "ok" }))
}

pub async fn serve() -> anyhow::Result<()> {
    let port: u16 = std::env::var("PORT")
        .ok()
        .and_then(|p| p.parse().ok())
        .unwrap_or(8080);
    let listener = tokio::net::TcpListener::bind(("0.0.0.0", port)).await?;
    tracing::info!("web server listening on port {port}");
    axum::serve(listener, router()).await?;
    Ok(())
}
