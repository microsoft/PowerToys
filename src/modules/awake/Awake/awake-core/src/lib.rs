//! Platform-independent logic for PowerToys Awake.
//!
//! Everything here is deterministic: time, settings files and user input arrive as values, and
//! decisions leave as data. The `awake` binary owns every Windows call, which keeps this crate
//! fully unit-testable.

#![forbid(unsafe_code)]

pub mod cli;
pub mod console;
pub mod duration;
pub mod engine;
pub mod execution_state;
pub mod mode;
pub mod powertoys;
pub mod settings;
pub mod strings;
pub mod time;
pub mod tray;
