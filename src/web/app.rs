mod alive;
mod auth;
mod dashboard;
mod health;
mod styles;
mod voice;

use topcoat::{
    Result,
    cookie::RouterBuilderCookieExt,
    router::{Router, layout},
    view::view,
};

use super::theme::theme_script;

pub fn router(state: super::State) -> anyhow::Result<Router> {
    Ok(topcoat::router::module_router!()
        .cookies()
        .app_context(state)
        .build())
}

#[layout]
async fn root_layout(slot: Result) -> Result {
    view! {
        <!DOCTYPE html>
        <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width,initial-scale=1">
                <title>"Wasabi Bot"</title>
                theme_script()
                <link rel="stylesheet" href="/styles">
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fontsource/geist@5.2.8/400.css">
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fontsource/geist@5.2.8/500.css">
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fontsource/geist@5.2.8/600.css">
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fontsource/geist@5.2.8/700.css">
            </head>
            <body>(slot?)</body>
        </html>
    }
}
