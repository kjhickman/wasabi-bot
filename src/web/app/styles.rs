use topcoat::{Result, router::route};

#[route(GET)]
async fn styles() -> Result<([(&'static str, &'static str); 1], &'static str)> {
    Ok((
        [("content-type", "text/css; charset=utf-8")],
        include_str!(concat!(env!("OUT_DIR"), "/tailwind.css")),
    ))
}
