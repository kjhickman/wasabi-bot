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
                <link rel="stylesheet" href=(format!("/styles?v={}", env!("WASABI_CSS_VERSION")))>
                <link rel="preconnect" href="https://cdn.jsdelivr.net" crossorigin="anonymous">
                <link rel="preconnect" href="https://cdn.discordapp.com" crossorigin="anonymous">
                <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@fontsource-variable/geist@5.2.8/index.css">
            </head>
            <body>(slot?)</body>
        </html>
    }
}
