//! Shared PowerToys C++ code reached through `native/bridge.cpp`: group policy, telemetry and
//! kernel object names.

use awake_core::mode::Mode;

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
    // SAFETY: the C++ side returns a pointer to a static NUL-terminated constant.
    unsafe {
        let name = awake_exit_event_name();
        let len = (0..).take_while(|&i| *name.add(i) != 0).count();
        String::from_utf16_lossy(std::slice::from_raw_parts(name, len))
    }
}

/// Telemetry through the shared PowerToys provider. Events are only written when the user has
/// opted in to diagnostic data; the C++ side checks that on every write.
#[derive(Debug)]
pub struct Telemetry {
    version: Vec<u16>,
}

impl Telemetry {
    pub fn start() -> Telemetry {
        // SAFETY: registers the static provider once; balanced in Drop.
        unsafe { awake_telemetry_register() };
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
        // SAFETY: balances `start`.
        unsafe { awake_telemetry_unregister() };
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
    fn policy_reader_answers() {
        // Unconfigured on a developer machine; the point is that the C++ reader runs and maps.
        let _ = policy();
    }
}
