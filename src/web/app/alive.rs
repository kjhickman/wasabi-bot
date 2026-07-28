use topcoat::{Result, router::route};

#[route(GET)]
async fn alive() -> Result<&'static str> {
    Ok("ok")
}
