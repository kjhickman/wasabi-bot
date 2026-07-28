use topcoat::{Result, router::route};

#[route(GET)]
async fn styles() -> Result<([(&'static str, &'static str); 2], &'static str)> {
    Ok((
        [
            ("content-type", "text/css; charset=utf-8"),
            ("cache-control", "public, max-age=31536000, immutable"),
        ],
        include_str!(concat!(env!("OUT_DIR"), "/tailwind.css")),
    ))
}
