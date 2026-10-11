//! UTF-16 strings for Win32.

use std::ffi::OsStr;
use std::os::windows::ffi::OsStrExt;

/// A NUL-terminated UTF-16 copy of `text`.
pub fn wide(text: impl AsRef<OsStr>) -> Vec<u16> {
    text.as_ref().encode_wide().chain(Some(0)).collect()
}

/// Copies `text` into a fixed Win32 buffer, truncating on a character boundary and always
/// leaving room for the terminating NUL.
pub fn copy_truncated(text: &str, buffer: &mut [u16]) {
    let Some(capacity) = buffer.len().checked_sub(1) else {
        return;
    };
    let mut used = 0;
    for c in text.chars() {
        let mut units = [0u16; 2];
        let encoded = c.encode_utf16(&mut units);
        if used + encoded.len() > capacity {
            break;
        }
        buffer[used..used + encoded.len()].copy_from_slice(encoded);
        used += encoded.len();
    }
    buffer[used..].fill(0);
}

/// Reads a NUL-terminated or full-length UTF-16 buffer.
pub fn from_buffer(buffer: &[u16]) -> String {
    let end = buffer.iter().position(|&c| c == 0).unwrap_or(buffer.len());
    String::from_utf16_lossy(&buffer[..end])
}

/// Whether the NUL-terminated UTF-16 string at `ptr` equals `text`.
///
/// # Safety
///
/// `ptr` must point to a readable, NUL-terminated UTF-16 string.
pub unsafe fn nul_terminated_eq(ptr: *const u16, text: &str) -> bool {
    let mut offset = 0;
    for expected in text.encode_utf16() {
        // SAFETY: every unit up to and including the NUL is readable, and a mismatch, which the
        // NUL is for any unit of `text`, stops the loop.
        if unsafe { *ptr.add(offset) } != expected {
            return false;
        }
        offset += 1;
    }
    // SAFETY: as above; all earlier units matched non-NUL units, so this is still in bounds.
    unsafe { *ptr.add(offset) == 0 }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn nul_terminated_comparison() {
        let value = wide("ImmersiveColorSet");
        // SAFETY: `wide` strings are NUL-terminated.
        unsafe {
            assert!(nul_terminated_eq(value.as_ptr(), "ImmersiveColorSet"));
            assert!(!nul_terminated_eq(value.as_ptr(), "Immersive"));
            assert!(!nul_terminated_eq(value.as_ptr(), "ImmersiveColorSetX"));
            assert!(!nul_terminated_eq(wide("").as_ptr(), "a"));
        }
    }

    #[test]
    fn truncation_keeps_surrogate_pairs_whole() {
        let mut buffer = [0xFFFFu16; 4];
        copy_truncated("ab😀", &mut buffer);
        assert_eq!(
            from_buffer(&buffer),
            "ab",
            "the emoji needs two units and does not fit in the last one"
        );
        assert_eq!(buffer[3], 0);
    }

    #[test]
    fn exact_fit_and_empty_buffer() {
        let mut buffer = [0u16; 4];
        copy_truncated("abc", &mut buffer);
        assert_eq!(from_buffer(&buffer), "abc");
        copy_truncated("abc", &mut []);
    }
}
