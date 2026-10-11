//! Keeping a PowerToys session and `settings.json` in step: reading the file at startup and
//! after every change, and saving what the menu and the engine change.
//!
//! The runner, the Settings app and Command Palette write the same file, so a read may find it
//! half written and a save may find it locked; both retry on a window timer instead of blocking
//! the window thread.

use std::fs;
use std::path::Path;
use std::time::SystemTime;

use awake_core::engine::{Effect, Save};
use awake_core::settings::{AwakeSettings, ModeSetting, SettingsDocument};

use super::{App, Outcome};
use crate::log_lines;
use crate::platform::last_error;
use crate::platform::messages::{TIMER_RELOAD, TIMER_WRITE};
use crate::platform::settings_store::{LoadError, SaveError};
use crate::platform::window;

/// Settings writers often touch the file several times in a row; read once they settle.
const RELOAD_DEBOUNCE_MS: u32 = 50;
const RETRY_DELAY_MS: u32 = 100;
const MAX_RELOAD_RETRIES: u32 = 5;
const MAX_WRITE_RETRIES: u32 = 20;

/// What Awake last found at `settings.json`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum SettingsFile {
    /// Not read or saved yet, or its write time could not be read.
    Unseen,
    /// Read or saved when it had this write time. Every writer changes it, so finding the same
    /// time again means a version already acted on, Awake's own save echoing back included.
    Seen(SystemTime),
    /// The last read found no file; its removal is logged once.
    Missing,
}

impl SettingsFile {
    fn seen(path: &Path) -> SettingsFile {
        write_time(path).map_or(SettingsFile::Unseen, SettingsFile::Seen)
    }
}

impl App {
    pub(super) fn load_initial_settings(&mut self) -> Vec<Effect> {
        let Some(store) = &self.store else {
            return Vec::new();
        };
        let now = self.clock.now();
        let version = write_time(store.path());
        match store.read() {
            Ok(document) => match document.settings(&self.clock) {
                Ok(settings) => return self.apply_loaded(&settings, version),
                Err(error) => log::error!("Ignoring the settings file: {error}"),
            },
            Err(LoadError::Missing) => {
                log::info!("No settings file yet; creating one with defaults.");
                match store.write(&SettingsDocument::scaffold(now)) {
                    Ok(()) => self.settings_file = SettingsFile::seen(store.path()),
                    Err(error) => log::error!("Could not create the settings file: {error}"),
                }
            }
            // Never replace a file that exists but cannot be read: other programs own it too.
            Err(error) => log::error!("Ignoring the settings file: {error}"),
        }
        Vec::new()
    }

    /// Hands settings read from the file to the engine, at startup and on every reload alike.
    /// `version` is the file's write time, taken before reading it.
    fn apply_loaded(&mut self, settings: &AwakeSettings, version: Option<SystemTime>) -> Vec<Effect> {
        if let ModeSetting::Unknown(value) = settings.mode {
            log::warn!("{}", log_lines::unknown_mode(value));
        }
        let save = match version {
            Some(time) if self.settings_file == SettingsFile::Seen(time) => Save::Known,
            _ => Save::New,
        };
        self.settings_file = version.map_or(SettingsFile::Unseen, SettingsFile::Seen);
        self.engine.apply_saved_settings(settings, self.clock.now(), save)
    }

    /// The waiter saw the file change; read it once the writer is likely done.
    pub(super) fn settings_changed(&mut self) -> Outcome {
        if window::set_timer(self.window, TIMER_RELOAD, RELOAD_DEBOUNCE_MS) {
            Outcome::Handled(0)
        } else {
            self.reload_settings()
        }
    }

    pub(super) fn reload_settings(&mut self) -> Outcome {
        if let Some(waiter) = &self.waiter {
            waiter.settings_handled();
        }
        let Some(store) = &self.store else {
            return Outcome::Handled(0);
        };
        // Taken before the content: a save landing in between then looks known, and its own
        // change notification brings it back as new.
        let version = write_time(store.path());
        // Unsaved changes from the menu win over what the file says until they are written.
        let loaded = match store.read() {
            Ok(mut document) => {
                document.apply(&self.pending_changes);
                document.settings(&self.clock).map_err(|e| e.to_string())
            }
            // Not a writer halfway through, so no retries. Saving from the menu creates the file
            // again, as at startup; until then nothing says the mode should change.
            Err(LoadError::Missing) => {
                self.reload_retries = 0;
                if self.settings_file != SettingsFile::Missing {
                    self.settings_file = SettingsFile::Missing;
                    log::info!("{}", log_lines::SETTINGS_REMOVED);
                }
                return Outcome::Handled(0);
            }
            Err(error) => Err(error.to_string()),
        };
        match loaded {
            Ok(settings) => {
                self.reload_retries = 0;
                let effects = self.apply_loaded(&settings, version);
                if !effects.is_empty() {
                    log::info!("Settings changed: {effects:?}");
                }
                self.apply(effects)
            }
            // A writer may be halfway through; look again shortly, then give up quietly.
            Err(error) if self.reload_retries < MAX_RELOAD_RETRIES => {
                self.reload_retries += 1;
                log::debug!(
                    "Settings not readable yet ({error}); retry {}.",
                    self.reload_retries
                );
                if window::set_timer(self.window, TIMER_RELOAD, RETRY_DELAY_MS) {
                    Outcome::Handled(0)
                } else {
                    self.reload_retries = 0;
                    log::warn!("Keeping the current mode; could not schedule another read: {error}");
                    Outcome::Handled(0)
                }
            }
            Err(error) => {
                self.reload_retries = 0;
                log::warn!("{} {error}", log_lines::SETTINGS_UNREADABLE);
                Outcome::Handled(0)
            }
        }
    }

    /// Re-reads the file and applies the pending changes on top, so edits other programs made
    /// in the meantime are kept.
    ///
    /// This runs on the window thread on purpose, flush to disk included: the file is small,
    /// writes are rare and follow a user's choice or a session ending, and the runner and the
    /// Settings app read the result, so it has to survive a power cut. Keeping it here also keeps
    /// the pending changes and retries in one place. See `settings_store` for the details.
    pub(super) fn write_pending(&mut self) {
        let Some(store) = &self.store else {
            self.pending_changes.clear();
            return;
        };
        if self.pending_changes.is_empty() {
            return;
        }
        let now = self.clock.now();
        let mut document = match store.read() {
            Ok(document) => document,
            Err(LoadError::Missing) => SettingsDocument::scaffold(now),
            // Probably another writer mid-write; never replace a file others own with defaults.
            Err(error) if self.write_retries < MAX_WRITE_RETRIES => {
                self.write_retries += 1;
                log::debug!(
                    "Settings not readable before saving ({error}); retry {}.",
                    self.write_retries
                );
                self.schedule_write_retry();
                return;
            }
            Err(error) => {
                log::error!(
                    "Not saving {:?}: the settings file stays unreadable: {error}",
                    self.pending_changes
                );
                self.pending_changes.clear();
                self.write_retries = 0;
                return;
            }
        };
        if !document.apply(&self.pending_changes) {
            self.pending_changes.clear();
            return;
        }
        match store.write(&document) {
            Ok(()) => {
                log::info!("{} {:?}.", log_lines::SAVED, self.pending_changes);
                self.settings_file = SettingsFile::seen(store.path());
                self.pending_changes.clear();
                self.write_retries = 0;
            }
            Err(SaveError::Busy) if self.write_retries < MAX_WRITE_RETRIES => {
                self.write_retries += 1;
                self.schedule_write_retry();
            }
            Err(error) => {
                log::error!("{} {error}", log_lines::SAVE_FAILED);
                self.pending_changes.clear();
                self.write_retries = 0;
            }
        }
    }

    /// One last try at a save still waiting out a busy file, without retries: the retry timer
    /// dies with the window, and the next start would read a session that has already ended.
    pub(super) fn save_before_exit(&mut self) {
        if !self.pending_changes.is_empty() {
            self.write_retries = MAX_WRITE_RETRIES;
            self.write_pending();
        }
    }

    /// Without a timer nothing would ever write the pending changes, so give up on them visibly
    /// rather than leave them queued.
    fn schedule_write_retry(&mut self) {
        if !window::set_timer(self.window, TIMER_WRITE, RETRY_DELAY_MS) {
            log::error!(
                "Not saving {:?}: could not schedule a retry: {}",
                self.pending_changes,
                last_error()
            );
            self.pending_changes.clear();
            self.write_retries = 0;
        }
    }
}

fn write_time(path: &Path) -> Option<SystemTime> {
    fs::metadata(path).and_then(|metadata| metadata.modified()).ok()
}
