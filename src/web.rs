mod app;
#[allow(dead_code)]
mod components;
mod theme;

pub use app::router;

pub async fn serve() -> anyhow::Result<()> {
    let port: u16 = std::env::var("PORT")
        .ok()
        .and_then(|p| p.parse().ok())
        .unwrap_or(8080);
    let listener = tokio::net::TcpListener::bind(("0.0.0.0", port)).await?;
    tracing::info!("web server listening on port {port}");
    topcoat::serve_until(listener, router()?, std::future::pending::<()>()).await?;
    Ok(())
}
