//! One Awake per session.

use std::io;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::ptr;
use std::time::Duration;

use windows_sys::Win32::Foundation::{WAIT_ABANDONED, WAIT_OBJECT_0, WAIT_TIMEOUT};
use windows_sys::Win32::System::Threading::{CreateMutexW, ReleaseMutex, WaitForSingleObject};

use super::{last_error, wide::wide};

/// Holds the single-instance mutex; released when dropped.
#[derive(Debug)]
pub struct InstanceLock {
    mutex: OwnedHandle,
}

impl Drop for InstanceLock {
    fn drop(&mut self) {
        // SAFETY: the mutex is owned by this thread (acquired in `acquire`, dropped on the same
        // thread by the app), and the handle stays valid until `OwnedHandle` closes it.
        unsafe { ReleaseMutex(self.mutex.as_raw_handle()) };
    }
}

/// Waits up to `wait` for the previous instance to exit. `Ok(None)` means another instance
/// still runs.
pub fn acquire(name: &str, wait: Duration) -> io::Result<Option<InstanceLock>> {
    let name = wide(name);
    // SAFETY: `name` is NUL-terminated and outlives the call.
    let raw = unsafe { CreateMutexW(ptr::null(), 0, name.as_ptr()) };
    if raw.is_null() {
        return Err(last_error());
    }
    // SAFETY: `raw` is a fresh handle we own.
    let mutex = unsafe { OwnedHandle::from_raw_handle(raw) };
    let millis = u32::try_from(wait.as_millis()).unwrap_or(u32::MAX - 1);
    // SAFETY: valid handle.
    match unsafe { WaitForSingleObject(mutex.as_raw_handle(), millis) } {
        // An abandoned mutex means the previous instance crashed; ownership is still ours.
        WAIT_OBJECT_0 | WAIT_ABANDONED => Ok(Some(InstanceLock { mutex })),
        WAIT_TIMEOUT => Ok(None),
        _ => Err(last_error()),
    }
}
