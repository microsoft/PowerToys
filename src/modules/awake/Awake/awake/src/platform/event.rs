//! Manual-reset events, named or anonymous.
//!
//! Every event Awake uses is manual reset, so a signal sent before anyone waits is never lost.
//! A named event is created, or opened when another process (the runner, or the Awake that
//! handed off) created it first; one another program planted is refused, see `security`.

use std::os::windows::io::{FromRawHandle, OwnedHandle};
use std::{io, ptr};

use windows_sys::Win32::Foundation::{ERROR_ALREADY_EXISTS, GetLastError, SetLastError};
use windows_sys::Win32::System::Threading::{CreateEventW, EVENT_MODIFY_STATE, OpenEventW, SetEvent};

use super::security::{self, Descriptor};
use super::{last_error, wide::wide};

/// Creates or opens a manual-reset event that starts unsignaled.
pub fn manual_reset(name: Option<&str>) -> io::Result<OwnedHandle> {
    let Some(name) = name else {
        // SAFETY: null attributes and no name make a private event no other process can open.
        let raw = unsafe { CreateEventW(ptr::null(), 1, 0, ptr::null()) };
        if raw.is_null() {
            return Err(last_error());
        }
        // SAFETY: fresh handle we own.
        return Ok(unsafe { OwnedHandle::from_raw_handle(raw) });
    };
    let descriptor = Descriptor::for_current_user()?;
    let attributes = descriptor.attributes();
    let wide_name = wide(name);
    // SAFETY: the attributes, their descriptor and the NUL-terminated name outlive the call. The
    // last error is cleared first so that only this call can leave ERROR_ALREADY_EXISTS.
    let (raw, existed) = unsafe {
        SetLastError(0);
        let raw = CreateEventW(&raw const attributes, 1, 0, wide_name.as_ptr());
        (raw, GetLastError() == ERROR_ALREADY_EXISTS)
    };
    if raw.is_null() {
        return Err(last_error());
    }
    // SAFETY: fresh handle we own.
    let event = unsafe { OwnedHandle::from_raw_handle(raw) };
    if existed {
        security::verify_existing(&event, name)?;
    }
    Ok(event)
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
    fn an_event_a_trusted_process_created_is_shared() {
        let name = format!(r"Local\AwakeEventTest-shared-{}", std::process::id());
        let first = manual_reset(Some(&name)).unwrap();
        let second = manual_reset(Some(&name)).unwrap();
        signal_named(&name);
        assert!(is_set(&first) && is_set(&second));
    }

    #[test]
    fn an_event_planted_at_low_integrity_is_refused() {
        let name = format!(r"Local\AwakeEventTest-planted-{}", std::process::id());
        let planted = crate::platform::security::tests::low_integrity();
        let attributes = planted.attributes();
        let wide_name = wide(&name);
        // SAFETY: the attributes and name outlive the call.
        let raw = unsafe { CreateEventW(&raw const attributes, 1, 0, wide_name.as_ptr()) };
        assert!(!raw.is_null());
        // SAFETY: fresh handle we own.
        let _planted = unsafe { OwnedHandle::from_raw_handle(raw) };
        assert_eq!(
            manual_reset(Some(&name)).unwrap_err().kind(),
            io::ErrorKind::PermissionDenied
        );
    }

    #[test]
    fn signaling_a_missing_event_does_nothing() {
        signal_named(r"Local\AwakeEventTest-nobody-created-this");
        assert!(!is_set(&manual_reset(None).unwrap()));
    }
}
