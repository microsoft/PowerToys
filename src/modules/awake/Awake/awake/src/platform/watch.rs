//! Watching one file for changes through its folder, with overlapped `ReadDirectoryChangesW`.
//!
//! Only the waiter thread uses it; see `waiter`.

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::Path;
use std::{io, mem, ptr, slice};

use windows_sys::Win32::Foundation::{HANDLE, INVALID_HANDLE_VALUE};
use windows_sys::Win32::Storage::FileSystem::{
    CreateFileW, FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OVERLAPPED, FILE_LIST_DIRECTORY,
    FILE_NOTIFY_CHANGE_CREATION, FILE_NOTIFY_CHANGE_FILE_NAME, FILE_NOTIFY_CHANGE_LAST_WRITE,
    FILE_NOTIFY_CHANGE_SIZE, FILE_NOTIFY_INFORMATION, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    OPEN_EXISTING, ReadDirectoryChangesW,
};
use windows_sys::Win32::System::IO::{CancelIoEx, GetOverlappedResult, OVERLAPPED};
use windows_sys::Win32::System::Threading::ResetEvent;

use super::{last_error, wide::wide};

/// Bytes of change records one request can return.
const WATCH_BUFFER_BYTES: usize = 8192;

/// The buffer `ReadDirectoryChangesW` fills; records must be DWORD-aligned.
type WatchBuffer = [u32; WATCH_BUFFER_BYTES / 4];

/// Overlapped `ReadDirectoryChangesW` on one directory, filtered to one file name.
///
/// The OVERLAPPED and the buffer are written by the kernel while a request is pending, so they
/// are held as raw pointers from `Box::into_raw` rather than as `Box`es, which would claim unique
/// access to memory something else is writing. They are freed in `Drop` only once the request
/// has been cancelled and has completed. The raw pointers also keep the type `!Send`: it is
/// created, serviced and dropped on the waiter thread, the thread that issued the request.
pub struct DirectoryWatch {
    directory: OwnedHandle,
    event: OwnedHandle,
    file_name: Vec<u16>,
    overlapped: *mut OVERLAPPED,
    buffer: *mut WatchBuffer,
    armed: bool,
}

impl DirectoryWatch {
    /// Signaled when the pending request completes.
    pub fn event(&self) -> HANDLE {
        self.event.as_raw_handle()
    }

    /// Watches `directory` for changes to `file_name`, with the first request already issued.
    pub fn start(directory: &Path, file_name: &OsString) -> io::Result<Self> {
        let path = wide(directory);
        // SAFETY: `path` is NUL-terminated; directory handles need FILE_FLAG_BACKUP_SEMANTICS.
        let raw = unsafe {
            CreateFileW(
                path.as_ptr(),
                FILE_LIST_DIRECTORY,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                ptr::null(),
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OVERLAPPED,
                ptr::null_mut(),
            )
        };
        if raw == INVALID_HANDLE_VALUE {
            return Err(last_error());
        }
        let mut watch = DirectoryWatch {
            // SAFETY: fresh handle we own.
            directory: unsafe { OwnedHandle::from_raw_handle(raw) },
            event: super::event::manual_reset(None)?,
            file_name: file_name.encode_wide().map(lower).collect(),
            // SAFETY: OVERLAPPED is plain data, for which all zeroes is a valid value.
            overlapped: Box::into_raw(Box::new(unsafe { mem::zeroed() })),
            buffer: Box::into_raw(Box::new([0; WATCH_BUFFER_BYTES / 4])),
            armed: false,
        };
        watch.arm()?;
        Ok(watch)
    }

    /// Issues the next request; call once the previous one has completed.
    pub fn arm(&mut self) -> io::Result<()> {
        debug_assert!(!self.armed, "a request is already pending");
        // SAFETY: valid event handle.
        unsafe { ResetEvent(self.event.as_raw_handle()) };
        // SAFETY: no request is pending, so nothing else uses the OVERLAPPED; the pointer came
        // from `Box::into_raw` and stays valid until `Drop`.
        unsafe {
            self.overlapped.write(OVERLAPPED {
                hEvent: self.event.as_raw_handle(),
                // SAFETY: OVERLAPPED is plain data, for which all zeroes is a valid value.
                ..mem::zeroed()
            });
        }
        let filter = FILE_NOTIFY_CHANGE_FILE_NAME
            | FILE_NOTIFY_CHANGE_LAST_WRITE
            | FILE_NOTIFY_CHANGE_SIZE
            | FILE_NOTIFY_CHANGE_CREATION;
        // SAFETY: the buffer and OVERLAPPED are live heap allocations that are only reached
        // through these raw pointers while the request is pending; `Drop` cancels the request
        // and waits for it to complete before freeing them.
        let ok = unsafe {
            ReadDirectoryChangesW(
                self.directory.as_raw_handle(),
                self.buffer.cast(),
                WATCH_BUFFER_BYTES as u32,
                0,
                filter,
                ptr::null_mut(),
                self.overlapped,
                None,
            )
        } != 0;
        self.armed = ok;
        if ok { Ok(()) } else { Err(last_error()) }
    }

    /// Whether the completed batch mentions the watched file. An overflowed batch counts as a
    /// change because its details were lost.
    pub fn take_relevant_change(&mut self) -> bool {
        let mut bytes = 0u32;
        // SAFETY: the request completed (its event is signaled), so no wait is needed and the
        // kernel no longer writes to the OVERLAPPED.
        let ok =
            unsafe { GetOverlappedResult(self.directory.as_raw_handle(), self.overlapped, &mut bytes, 0) }
                != 0;
        self.armed = false;
        if !ok || bytes == 0 {
            return true;
        }
        let len = (bytes as usize).min(WATCH_BUFFER_BYTES);
        // SAFETY: the request completed, so the kernel is done with the buffer until the next
        // `arm`, which needs `&mut self` and so cannot overlap this borrow. It holds at least
        // `len` initialized bytes.
        let records = unsafe { slice::from_raw_parts(self.buffer.cast::<u8>(), len) };
        batch_mentions(records, &self.file_name)
    }
}

impl Drop for DirectoryWatch {
    fn drop(&mut self) {
        if self.armed {
            let mut bytes = 0;
            // SAFETY: cancel the pending request and wait (bWait = TRUE) until it completes, after
            // which the kernel no longer touches the OVERLAPPED or the buffer.
            unsafe {
                CancelIoEx(self.directory.as_raw_handle(), self.overlapped);
                GetOverlappedResult(self.directory.as_raw_handle(), self.overlapped, &mut bytes, 1);
            }
        }
        // SAFETY: both came from `Box::into_raw` in `start`, are freed only here, and no request
        // is pending any more.
        unsafe {
            drop(Box::from_raw(self.overlapped));
            drop(Box::from_raw(self.buffer));
        }
    }
}

/// Whether a batch of `FILE_NOTIFY_INFORMATION` records names `file_name` (already lowercase).
/// Reads the fields byte by byte, so a malformed batch can end the walk early but never cause an
/// out-of-bounds or misaligned read.
fn batch_mentions(records: &[u8], file_name: &[u16]) -> bool {
    const NEXT: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, NextEntryOffset);
    const NAME_LENGTH: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, FileNameLength);
    const NAME: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, FileName);
    let read_u32 = |at: usize| -> Option<u32> {
        let bytes = records.get(at..at.checked_add(4)?)?;
        Some(u32::from_ne_bytes(bytes.try_into().ok()?))
    };
    let mut offset = 0usize;
    loop {
        debug_assert_eq!(
            offset % mem::align_of::<FILE_NOTIFY_INFORMATION>(),
            0,
            "the kernel aligns every record to a DWORD"
        );
        let (Some(next), Some(name_bytes)) = (read_u32(offset + NEXT), read_u32(offset + NAME_LENGTH)) else {
            return false;
        };
        let start = offset + NAME;
        let Some(name) = start
            .checked_add(name_bytes as usize)
            .and_then(|end| records.get(start..end))
        else {
            return false;
        };
        let units = name
            .chunks_exact(2)
            .map(|pair| u16::from_ne_bytes([pair[0], pair[1]]));
        if name.len() == file_name.len() * 2 && units.map(lower).eq(file_name.iter().copied()) {
            return true;
        }
        if next == 0 {
            return false;
        }
        offset += next as usize;
    }
}

fn lower(unit: u16) -> u16 {
    if (u16::from(b'A')..=u16::from(b'Z')).contains(&unit) {
        unit + 32
    } else {
        unit
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// One `FILE_NOTIFY_INFORMATION` record, padded to a DWORD as the kernel lays them out.
    fn record(name: &str, last: bool) -> Vec<u8> {
        let name: Vec<u8> = name.encode_utf16().flat_map(u16::to_ne_bytes).collect();
        let size = (12 + name.len()).next_multiple_of(4);
        let mut bytes = Vec::with_capacity(size);
        bytes.extend_from_slice(&(if last { 0 } else { size as u32 }).to_ne_bytes());
        bytes.extend_from_slice(&3u32.to_ne_bytes()); // FILE_ACTION_MODIFIED
        bytes.extend_from_slice(&(name.len() as u32).to_ne_bytes());
        bytes.extend_from_slice(&name);
        bytes.resize(size, 0);
        bytes
    }

    fn lowered(name: &str) -> Vec<u16> {
        name.encode_utf16().map(lower).collect()
    }

    #[test]
    fn finds_the_watched_file_in_any_record_ignoring_ascii_case() {
        let target = lowered("settings.json");
        let mut batch = record("settings.json.tmp", false);
        batch.extend(record("Settings.JSON", true));
        assert!(batch_mentions(&batch, &target));
        assert!(!batch_mentions(&record("other.json", true), &target));
        assert!(!batch_mentions(&record("settings.jso", true), &target));
    }

    #[test]
    fn malformed_batches_end_the_walk_without_reading_out_of_bounds() {
        let target = lowered("settings.json");
        let full = record("settings.json", true);
        // Cut off inside the header and inside the name.
        assert!(!batch_mentions(&full[..8], &target));
        assert!(!batch_mentions(&full[..full.len() - 4], &target));
        // A name length running far past the end.
        let mut huge = record("a", true);
        huge[8..12].copy_from_slice(&u32::MAX.to_ne_bytes());
        assert!(!batch_mentions(&huge, &target));
        // A next offset that points past the end.
        let mut dangling = record("a", false);
        dangling[..4].copy_from_slice(&4096u32.to_ne_bytes());
        assert!(!batch_mentions(&dangling, &target));
        assert!(!batch_mentions(&[], &target));
    }
}
