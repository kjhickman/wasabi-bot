#![warn(clippy::pedantic)]
#![warn(clippy::nursery)]
#![warn(clippy::cargo)]
#![allow(clippy::missing_errors_doc)]

pub mod bot;
mod commands;
pub mod db;
mod llm;
pub mod telemetry;
pub mod web;
