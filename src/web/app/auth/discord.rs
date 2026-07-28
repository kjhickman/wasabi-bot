mod callback;

use topcoat::{
    Result,
    context::Cx,
    router::{error::SeeOther, route},
};

#[route(GET)]
async fn discord(cx: &Cx) -> Result<SeeOther> {
    crate::web::auth::login(cx).await
}
