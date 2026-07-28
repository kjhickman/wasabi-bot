use topcoat::{
    Result,
    context::Cx,
    router::{content::Form, error::SeeOther, route},
};

use crate::web::auth::CallbackQuery;

#[route(GET)]
async fn callback(cx: &Cx, query: Form<CallbackQuery>) -> Result<SeeOther> {
    crate::web::auth::callback(cx, query).await
}
