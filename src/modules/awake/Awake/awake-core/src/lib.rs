//! Platform-independent logic for PowerToys Awake.
//!
//! Everything here is deterministic: time, settings files and user input arrive as values, and
//! decisions leave as data. The `awake` binary owns every Windows call, which keeps this crate
//! fully unit-testable.

#![forbid(unsafe_code)]
// The platform layer programs against this crate alone, so its rustdoc is the contract.
#![warn(missing_docs)]

pub mod cli;
pub mod duration;
pub mod engine;
pub mod execution_state;
pub mod mode;
pub mod powertoys;
pub mod settings;
pub mod strings;
pub mod text;
pub mod time;
pub mod tray_model;
