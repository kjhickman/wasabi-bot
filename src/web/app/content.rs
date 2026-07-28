use topcoat::{
    Result,
    context::Cx,
    router::{error::forbidden, route},
    view::{View, view},
};

use super::dashboard::{dashboard_content, dashboard_state};

#[route(GET)]
async fn content(cx: &Cx) -> Result<([(&'static str, &'static str); 1], View)> {
    let (session, bot) = dashboard_state(cx).await?;
    let session = session.ok_or_else(forbidden)?;
    let fragment = view! {
        <div id="dashboard-content">
            dashboard_content(session: Some(&session), bot: bot.as_ref())
        </div>
    }?;
    Ok(([("cache-control", "no-store")], fragment))
}
