//! One Awake per session.
//!
//! The mutex is created for the current user only, and one that another program created first
//! is refused rather than waited on; see `security`.

use std::io;
use std::marker::PhantomData;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::time::Duration;

use windows_sys::Win32::Foundation::{
    ERROR_ALREADY_EXISTS, GetLastError, SetLastError, WAIT_ABANDONED, WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::System::Threading::{CreateMutexW, ReleaseMutex, WaitForSingleObject};

use super::security::{self, Descriptor};
use super::{last_error, wide::wide};

/// Holds the single-instance mutex; released when dropped.
///
/// A mutex belongs to the thread that acquired it and `ReleaseMutex` fails on any other, so the
/// lock is neither `Send` nor `Sync`: dropping it elsewhere is a compile error, not a silent
/// leak until the acquiring thread exits.
#[derive(Debug)]
pub struct InstanceLock {
    mutex: OwnedHandle,
    _thread_affine: PhantomData<*const ()>,
}

impl Drop for InstanceLock {
    fn drop(&mut self) {
        // SAFETY: the type is `!Send`, so this runs on the thread that acquired the mutex in
        // `acquire`, and the handle stays valid until `OwnedHandle` closes it.
        unsafe { ReleaseMutex(self.mutex.as_raw_handle()) };
    }
}

/// Waits up to `wait` for the previous instance to exit. `Ok(None)` means another instance
/// still runs; a mutex another program planted under the name is an access-denied error.
pub fn acquire(name: &str, wait: Duration) -> io::Result<Option<InstanceLock>> {
    let descriptor = Descriptor::for_current_user()?;
    let attributes = descriptor.attributes();
    let wide_name = wide(name);
    // SAFETY: the attributes, their descriptor and the NUL-terminated name outlive the call. The
    // last error is cleared first so that only this call can leave ERROR_ALREADY_EXISTS.
    let (raw, existed) = unsafe {
        SetLastError(0);
        let raw = CreateMutexW(&raw const attributes, 0, wide_name.as_ptr());
        (raw, GetLastError() == ERROR_ALREADY_EXISTS)
    };
    if raw.is_null() {
        return Err(last_error());
    }
    // SAFETY: `raw` is a fresh handle we own.
    let mutex = unsafe { OwnedHandle::from_raw_handle(raw) };
    if existed {
        security::verify_existing(&mutex, name)?;
    }
    let millis = u32::try_from(wait.as_millis()).unwrap_or(u32::MAX - 1);
    // SAFETY: valid handle.
    match unsafe { WaitForSingleObject(mutex.as_raw_handle(), millis) } {
        // An abandoned mutex means the previous instance crashed; ownership is still ours.
        WAIT_OBJECT_0 | WAIT_ABANDONED => Ok(Some(InstanceLock {
            mutex,
            _thread_affine: PhantomData,
        })),
        WAIT_TIMEOUT => Ok(None),
        _ => Err(last_error()),
    }
}

#[cfg(test)]
mod tests {
    use windows_sys::Win32::Foundation::ERROR_ACCESS_DENIED;

    use super::*;

    fn name(test: &str) -> String {
        format!(r"Local\AwakeInstanceTest-{test}-{}", std::process::id())
    }

    #[test]
    fn a_second_acquire_sees_the_first_as_running() {
        let name = name("held");
        let _held = acquire(&name, Duration::ZERO)
            .unwrap()
            .expect("the first acquire wins");
        let other = std::thread::spawn(move || acquire(&name, Duration::ZERO).map(|lock| lock.is_none()));
        assert!(
            other.join().unwrap().unwrap(),
            "our own mutex is trusted, and held"
        );
    }

    #[test]
    fn a_mutex_planted_at_low_integrity_is_refused_without_waiting() {
        let name = name("planted");
        let planted = security::tests::low_integrity();
        let attributes = planted.attributes();
        let wide_name = wide(&name);
        // SAFETY: the attributes and name outlive the call. The planter owns the mutex, as a
        // squatting process would.
        let raw = unsafe { CreateMutexW(&raw const attributes, 1, wide_name.as_ptr()) };
        assert!(!raw.is_null());
        // SAFETY: fresh handle we own.
        let _planted = unsafe { OwnedHandle::from_raw_handle(raw) };
        let started = std::time::Instant::now();
        let error = std::thread::spawn(move || acquire(&name, Duration::from_secs(30)).unwrap_err())
            .join()
            .unwrap();
        assert_eq!(error.raw_os_error(), Some(ERROR_ACCESS_DENIED as i32));
        assert!(started.elapsed() < Duration::from_secs(10));
    }
}
