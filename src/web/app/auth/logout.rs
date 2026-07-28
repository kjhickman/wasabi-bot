use topcoat::{
    Result,
    context::Cx,
    router::{content::Form, error::SeeOther, route},
};

use crate::web::auth::LogoutForm;

#[route(POST)]
async fn logout(cx: &Cx, form: Form<LogoutForm>) -> Result<SeeOther> {
    crate::web::auth::logout(cx, form).await
}
