use topcoat::{Result, router::route};

#[route(GET)]
async fn favicon() -> Result<([(&'static str, &'static str); 2], &'static [u8])> {
    Ok((
        [
            ("content-type", "image/png"),
            ("cache-control", "public, max-age=31536000, immutable"),
        ],
        include_bytes!("../favicon.png").as_slice(),
    ))
}
