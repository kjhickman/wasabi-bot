mod alive;
mod dashboard;
mod health;

use topcoat::{
    Result,
    asset::{AssetBundle, RouterBuilderAssetExt},
    font::{RouterBuilderFontExt, fontsource::fontsource_font},
    router::{Router, layout},
    view::view,
};

use super::theme::theme_script;

pub fn router() -> anyhow::Result<Router> {
    Ok(topcoat::router::module_router!()
        .assets(AssetBundle::load()?)
        .discover_fonts()
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
                <link rel="stylesheet" href=(topcoat::tailwind::stylesheet!())>
                topcoat::font::link(
                    font: fontsource_font!(
                        GEIST, weight : [400, 500, 600, 700], style : Normal
                    ),
                    preload: false
                )
                topcoat::dev::script()
            </head>
            <body>(slot?)</body>
        </html>
    }
}
