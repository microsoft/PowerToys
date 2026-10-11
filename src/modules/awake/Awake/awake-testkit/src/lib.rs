//! What the Awake tests and the `measure` example share: starting Awake the way the PowerToys
//! runner does, finding and measuring its process, reading its logs, and the names of its test
//! hooks and of the log lines the tests wait for.
//!
//! The last two are the binary's own files, included here, so a rename on either side is a
//! compile error rather than a test that times out.

#[path = "../../awake/src/hook_names.rs"]
pub mod hook_names;
#[path = "../../awake/src/log_lines.rs"]
pub mod log_lines;
pub mod process;
pub mod runner;

use std::fs;
use std::path::Path;

/// `text` as a NUL-terminated UTF-16 string.
pub fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(Some(0)).collect()
}

/// Every log file under `log_dir` (`...\Awake\Logs`, one folder per version), concatenated.
pub fn read_logs(log_dir: &Path) -> String {
    let mut text = String::new();
    for version in fs::read_dir(log_dir).into_iter().flatten().flatten() {
        for file in fs::read_dir(version.path()).into_iter().flatten().flatten() {
            text.push_str(&fs::read_to_string(file.path()).unwrap_or_default());
        }
    }
    text
}
