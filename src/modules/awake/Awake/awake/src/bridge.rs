//! Shared PowerToys C++ code reached through `native/bridge.cpp`: group policy, telemetry and
//! kernel object names.

use std::sync::{Mutex, MutexGuard, PoisonError};

use awake_core::mode::Mode;

use crate::platform::wide::from_nul_terminated;

unsafe extern "C" {
    fn awake_telemetry_register();
    fn awake_telemetry_unregister();
    fn awake_telemetry_cli_command(successful: bool, version: *const u16);
    fn awake_telemetry_mode_entered(mode: i32, version: *const u16);
    fn awake_gpo_enabled_configuration() -> i32;
    fn awake_exit_event_name() -> *const u16;
}

/// The group policy state for Awake, as `powertoys_gpo::gpo_rule_configured_t` defines it.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Policy {
    WrongValue,
    Unavailable,
    NotConfigured,
    Disabled,
    Enabled,
}

pub fn policy() -> Policy {
    // SAFETY: no arguments; the C++ side catches every exception.
    match unsafe { awake_gpo_enabled_configuration() } {
        -3 => Policy::WrongValue,
        -2 => Policy::Unavailable,
        0 => Policy::Disabled,
        1 => Policy::Enabled,
        _ => Policy::NotConfigured,
    }
}

/// `Local\PowerToysAwakeExitEvent-...`, the event the runner signals to stop Awake.
pub fn exit_event_name() -> String {
    // SAFETY: no arguments; the C++ side returns a non-null pointer to a static NUL-terminated
    // constant, which stays valid for the process lifetime.
    unsafe { from_nul_terminated(awake_exit_event_name()) }
}

/// How many `Telemetry` values are alive. The provider is a single static handle that must not
/// be registered twice, so only the first value registers it and only the last one unregisters.
/// The lock also keeps a register from racing an unregister.
static LIVE: Mutex<usize> = Mutex::new(0);

fn live() -> MutexGuard<'static, usize> {
    // The count is updated in one step, so a panic elsewhere cannot leave it half changed.
    LIVE.lock().unwrap_or_else(PoisonError::into_inner)
}

/// Telemetry through the shared PowerToys provider. Events are only written when the user has
/// opted in to diagnostic data; the C++ side checks that on every write.
#[derive(Debug)]
pub struct Telemetry {
    version: Vec<u16>,
}

impl Telemetry {
    pub fn start() -> Telemetry {
        let mut live = live();
        if *live == 0 {
            // SAFETY: no arguments; the count is zero under the lock, so the provider is not
            // registered. Balanced by the last Drop.
            unsafe { awake_telemetry_register() };
        }
        *live += 1;
        Telemetry {
            version: env!("AWAKE_TELEMETRY_VERSION")
                .encode_utf16()
                .chain(Some(0))
                .collect(),
        }
    }

    pub fn cli_command(&self, successful: bool) {
        // SAFETY: `version` is NUL-terminated and outlives the call.
        unsafe { awake_telemetry_cli_command(successful, self.version.as_ptr()) };
    }

    pub fn mode_entered(&self, mode: Mode) {
        // SAFETY: as above.
        unsafe { awake_telemetry_mode_entered(mode.value() as i32, self.version.as_ptr()) };
    }
}

impl Drop for Telemetry {
    fn drop(&mut self) {
        let mut live = live();
        *live -= 1;
        if *live == 0 {
            // SAFETY: no arguments; this was the last live value, so the provider is registered
            // and nothing else will write through it until the next `start`.
            unsafe { awake_telemetry_unregister() };
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn exit_event_name_comes_from_shared_constants() {
        assert_eq!(
            exit_event_name(),
            "Local\\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe"
        );
    }

    #[test]
    fn telemetry_registers_the_provider_once_while_any_value_lives() {
        // Other tests do not touch telemetry, so the count starts and ends at zero here.
        let first = Telemetry::start();
        let second = Telemetry::start();
        assert_eq!(*live(), 2);
        drop(first);
        assert_eq!(
            *live(),
            1,
            "the provider stays registered for the remaining value"
        );
        drop(second);
        assert_eq!(*live(), 0);
        drop(Telemetry::start());
        assert_eq!(
            *live(),
            0,
            "a new value registers again after the last one is gone"
        );
    }

    #[test]
    fn policy_reader_answers() {
        // Unconfigured on a developer machine; the point is that the C++ reader runs and maps.
        let _ = policy();
    }
}
