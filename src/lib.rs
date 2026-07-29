#![warn(clippy::pedantic)]
#![warn(clippy::nursery)]
#![warn(clippy::cargo)]
#![allow(clippy::missing_errors_doc)]

pub mod bot;
mod commands;
pub mod db;
mod llm;
mod music;
pub mod telemetry;
pub mod ui_events;
pub(crate) mod voice;
pub mod web;
