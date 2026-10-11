//! Manual-reset events, named or anonymous.
//!
//! Every event Awake uses is manual reset, so a signal sent before anyone waits is never lost.
//! A named event is created, or opened when another process (the runner, or the Awake that
//! handed off) created it first.

use std::os::windows::io::{FromRawHandle, OwnedHandle};
use std::{io, ptr};

use windows_sys::Win32::System::Threading::{CreateEventW, EVENT_MODIFY_STATE, OpenEventW, SetEvent};

use super::{last_error, wide::wide};

/// Creates or opens a manual-reset event that starts unsignaled.
pub fn manual_reset(name: Option<&str>) -> io::Result<OwnedHandle> {
    let name = name.map(wide);
    let name_ptr = name.as_ref().map_or(ptr::null(), Vec::as_ptr);
    // SAFETY: null attributes are allowed; the name is null or NUL-terminated and outlives the call.
    let raw = unsafe { CreateEventW(ptr::null(), 1, 0, name_ptr) };
    if raw.is_null() {
        return Err(last_error());
    }
    // SAFETY: fresh handle we own.
    Ok(unsafe { OwnedHandle::from_raw_handle(raw) })
}

/// Signals an existing named event, if there is one; nobody waiting is not an error.
pub fn signal_named(name: &str) {
    let name = wide(name);
    // SAFETY: NUL-terminated name; the handle is closed right after signaling.
    unsafe {
        let event = OpenEventW(EVENT_MODIFY_STATE, 0, name.as_ptr());
        if !event.is_null() {
            SetEvent(event);
            drop(OwnedHandle::from_raw_handle(event));
        }
    }
}

#[cfg(test)]
mod tests {
    use std::os::windows::io::AsRawHandle;

    use windows_sys::Win32::Foundation::{WAIT_OBJECT_0, WAIT_TIMEOUT};
    use windows_sys::Win32::System::Threading::WaitForSingleObject;

    use super::*;

    fn is_set(event: &OwnedHandle) -> bool {
        // SAFETY: valid event handle; a zero timeout only polls.
        let result = unsafe { WaitForSingleObject(event.as_raw_handle(), 0) };
        assert!(result == WAIT_OBJECT_0 || result == WAIT_TIMEOUT);
        result == WAIT_OBJECT_0
    }

    #[test]
    fn a_named_signal_reaches_the_creator_and_stays_set() {
        let name = format!(r"Local\AwakeEventTest-{}", std::process::id());
        let event = manual_reset(Some(&name)).unwrap();
        assert!(!is_set(&event));
        signal_named(&name);
        // Manual reset: polling does not consume the signal.
        assert!(is_set(&event));
        assert!(is_set(&event));
    }

    #[test]
    fn signaling_a_missing_event_does_nothing() {
        signal_named(r"Local\AwakeEventTest-nobody-created-this");
        assert!(!is_set(&manual_reset(None).unwrap()));
    }
}
