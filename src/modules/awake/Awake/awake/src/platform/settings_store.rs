//! Reading and writing `settings.json` safely next to the other programs that use it.
//!
//! Reads never lock other writers out. Writes go to a temporary file first and replace the
//! original only once the new content is safely on disk, so a full disk or a crash cannot leave
//! a truncated file behind. The replacement's write time is then refreshed, because the Settings
//! app's file watchers only listen for write-time changes, not renames.

use std::fs::{self, File, OpenOptions};
use std::io::{self, Read, Write};
use std::os::windows::fs::OpenOptionsExt;
use std::path::{Path, PathBuf};
use std::time::SystemTime;

use awake_core::settings::{MAX_FILE_BYTES, SettingsDocument, SettingsError};
use windows_sys::Win32::Foundation::{ERROR_ACCESS_DENIED, ERROR_LOCK_VIOLATION, ERROR_SHARING_VIOLATION};
use windows_sys::Win32::Storage::FileSystem::{
    FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE, FILE_WRITE_ATTRIBUTES,
};

const SHARE_ALL: u32 = FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE;

#[derive(Debug)]
pub enum LoadError {
    Missing,
    Io(io::Error),
    Invalid(SettingsError),
}

impl std::fmt::Display for LoadError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            LoadError::Missing => f.write_str("the file does not exist"),
            LoadError::Io(error) => write!(f, "{error}"),
            LoadError::Invalid(error) => write!(f, "{error}"),
        }
    }
}

#[derive(Debug)]
pub enum SaveError {
    /// Another program has the file open right now; try again shortly.
    Busy,
    Io(io::Error),
}

impl std::fmt::Display for SaveError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SaveError::Busy => f.write_str("the file is in use"),
            SaveError::Io(error) => write!(f, "{error}"),
        }
    }
}

#[derive(Clone, Debug)]
pub struct SettingsStore {
    path: PathBuf,
}

impl SettingsStore {
    pub fn new(path: PathBuf) -> Self {
        SettingsStore { path }
    }

    pub fn path(&self) -> &Path {
        &self.path
    }

    pub fn read(&self) -> Result<SettingsDocument, LoadError> {
        let file = OpenOptions::new()
            .read(true)
            .share_mode(SHARE_ALL)
            .open(&self.path);
        let file = match file {
            Ok(file) => file,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Err(LoadError::Missing),
            Err(error) => return Err(LoadError::Io(error)),
        };
        let mut bytes = Vec::new();
        // Reading one byte past the limit is how an oversized file is detected.
        file.take(MAX_FILE_BYTES as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(LoadError::Io)?;
        SettingsDocument::parse(&bytes).map_err(LoadError::Invalid)
    }

    pub fn write(&self, document: &SettingsDocument) -> Result<(), SaveError> {
        let directory = self.path.parent().unwrap_or(Path::new("."));
        fs::create_dir_all(directory).map_err(SaveError::Io)?;
        let temporary = directory.join(format!("settings.json.{}.tmp", std::process::id()));

        let staged = write_durably(&temporary, &document.to_bytes());
        if let Err(error) = staged {
            let _ = fs::remove_file(&temporary);
            return Err(SaveError::Io(error));
        }
        if let Err(error) = fs::rename(&temporary, &self.path) {
            let _ = fs::remove_file(&temporary);
            return Err(if is_busy(&error) {
                SaveError::Busy
            } else {
                SaveError::Io(error)
            });
        }
        // Best effort: a failure here only delays the Settings app noticing the change.
        let _ = touch(&self.path);
        Ok(())
    }
}

fn write_durably(path: &Path, bytes: &[u8]) -> io::Result<()> {
    let mut file = OpenOptions::new()
        .write(true)
        .create(true)
        .truncate(true)
        .open(path)?;
    file.write_all(bytes)?;
    file.sync_all()
}

fn touch(path: &Path) -> io::Result<()> {
    let file: File = OpenOptions::new()
        .access_mode(FILE_WRITE_ATTRIBUTES)
        .share_mode(SHARE_ALL)
        .open(path)?;
    file.set_modified(SystemTime::now())
}

fn is_busy(error: &io::Error) -> bool {
    matches!(
        error.raw_os_error().map(|code| code as u32),
        Some(ERROR_SHARING_VIOLATION | ERROR_LOCK_VIOLATION | ERROR_ACCESS_DENIED)
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use awake_core::mode::Mode;
    use awake_core::settings::SettingsChange;
    use awake_core::time::{DateTimeOffset, Timestamp};

    fn temp_dir(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("awake-store-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    fn document() -> SettingsDocument {
        SettingsDocument::scaffold(DateTimeOffset::new(
            Timestamp::from_ticks(134_000_000_000_000_000),
            0,
        ))
    }

    #[test]
    fn write_then_read_round_trips_and_leaves_no_temp_files() {
        let dir = temp_dir("roundtrip");
        let store = SettingsStore::new(dir.join("Awake").join("settings.json"));
        let mut doc = document();
        doc.apply(&[SettingsChange::Mode(Mode::Indefinite)]);
        store.write(&doc).unwrap();
        assert_eq!(store.read().unwrap(), doc);
        let leftovers: Vec<_> = fs::read_dir(dir.join("Awake"))
            .unwrap()
            .flatten()
            .map(|e| e.file_name())
            .collect();
        assert_eq!(leftovers, ["settings.json"]);
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn missing_and_corrupt_files_are_reported_not_replaced() {
        let dir = temp_dir("corrupt");
        let path = dir.join("settings.json");
        let store = SettingsStore::new(path.clone());
        assert!(matches!(store.read(), Err(LoadError::Missing)));
        fs::write(&path, b"{\"name\": broken").unwrap();
        assert!(matches!(store.read(), Err(LoadError::Invalid(_))));
        assert_eq!(
            fs::read(&path).unwrap(),
            b"{\"name\": broken",
            "reading never rewrites the file"
        );
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn oversized_files_are_rejected_without_reading_them_whole() {
        let dir = temp_dir("large");
        let path = dir.join("settings.json");
        fs::write(&path, vec![b' '; MAX_FILE_BYTES + 10]).unwrap();
        assert!(matches!(
            SettingsStore::new(path).read(),
            Err(LoadError::Invalid(SettingsError::TooLarge))
        ));
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn reading_does_not_block_writers() {
        let dir = temp_dir("shared");
        let path = dir.join("settings.json");
        let store = SettingsStore::new(path.clone());
        store.write(&document()).unwrap();
        // Hold the file open the way Awake's reader does while another program writes it.
        let _reader = OpenOptions::new()
            .read(true)
            .share_mode(SHARE_ALL)
            .open(&path)
            .unwrap();
        fs::write(&path, document().to_bytes()).expect("in-place writers still succeed");
        store.write(&document()).expect("replacing writers still succeed");
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn a_reader_without_delete_sharing_makes_the_write_busy_not_corrupt() {
        let dir = temp_dir("busy");
        let path = dir.join("settings.json");
        let store = SettingsStore::new(path.clone());
        store.write(&document()).unwrap();
        let before = fs::read(&path).unwrap();
        // File.ReadAllText in .NET shares read only.
        let reader = OpenOptions::new()
            .read(true)
            .share_mode(FILE_SHARE_READ)
            .open(&path)
            .unwrap();
        let mut changed = document();
        changed.apply(&[SettingsChange::Mode(Mode::Timed)]);
        assert!(matches!(store.write(&changed), Err(SaveError::Busy)));
        drop(reader);
        assert_eq!(fs::read(&path).unwrap(), before);
        store.write(&changed).unwrap();
        assert_eq!(store.read().unwrap(), changed);
        fs::remove_dir_all(dir).ok();
    }
}

/// Used by `tools/Test-SettingsWatcher.ps1` to check that .NET file watchers, configured like
/// the Settings app's, see Awake's writes. Not part of the normal test run.
#[cfg(test)]
mod watcher_probe {
    use super::*;
    use awake_core::mode::Mode;
    use awake_core::settings::SettingsChange;
    use awake_core::time::{DateTimeOffset, Timestamp};

    #[test]
    #[ignore = "driven by tools/Test-SettingsWatcher.ps1"]
    fn write_for_external_watcher() {
        let dir = PathBuf::from(std::env::var("AWAKE_WATCH_TEST_DIR").expect("set AWAKE_WATCH_TEST_DIR"));
        let store = SettingsStore::new(dir.join("settings.json"));
        let mut document = SettingsDocument::scaffold(DateTimeOffset::new(
            Timestamp::from_ticks(134_000_000_000_000_000),
            0,
        ));
        for mode in [Mode::Indefinite, Mode::Passive, Mode::Timed] {
            document.apply(&[SettingsChange::Mode(mode)]);
            store.write(&document).unwrap();
            std::thread::sleep(std::time::Duration::from_millis(400));
        }
    }
}
