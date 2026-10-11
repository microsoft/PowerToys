//! Reading and writing `settings.json` safely next to the other programs that use it.
//!
//! Reads never lock other writers out. Writes go to a temporary file first and replace the
//! original only once the new content is safely on disk, so a full disk or a crash cannot leave
//! a truncated file behind. The replacement's write time is then refreshed, because the Settings
//! app's file watchers only listen for write-time changes, not renames.
//!
//! The module folder is writable by the user, and Awake may run elevated, so none of these steps
//! follows a link someone left in it: the temporary file is always a new file, and the write
//! time is set on `settings.json` itself. A temporary file a killed Awake left behind is removed
//! by the next write once it is clearly abandoned.
//!
//! Writes, including the flush to disk, run on the window thread, which also owns the
//! keep-awake request. That is a deliberate trade: the file is a few kilobytes, and writes are
//! rare and user-initiated (a menu choice, a timer ending, a one-time fix-up of an expired
//! time), so the wait is a few milliseconds and well inside every shutdown allowance. The flush
//! itself is what makes the rename safe: without it, a power cut can leave a renamed but empty
//! file, and the runner and the Settings app both read this file. Moving writes to another
//! thread would also move the retry and pending-change state `App` keeps in one place.

use std::ffi::OsStr;
use std::fs::{self, File, OpenOptions};
use std::io::{self, Read, Write};
use std::os::windows::fs::{MetadataExt, OpenOptionsExt};
use std::path::{Path, PathBuf};
use std::time::{Duration, SystemTime};

use awake_core::settings::{MAX_FILE_BYTES, SettingsDocument, SettingsError};
use windows_sys::Win32::Foundation::{ERROR_ACCESS_DENIED, ERROR_LOCK_VIOLATION, ERROR_SHARING_VIOLATION};
use windows_sys::Win32::Storage::FileSystem::{
    FILE_ATTRIBUTE_REPARSE_POINT, FILE_FLAG_OPEN_REPARSE_POINT, FILE_SHARE_DELETE, FILE_SHARE_READ,
    FILE_SHARE_WRITE, FILE_WRITE_ATTRIBUTES,
};

const SHARE_ALL: u32 = FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE;
/// A staged write takes milliseconds, so a temporary file this old belongs to an Awake that was
/// killed mid-write. The margin keeps a concurrent write by this user's Awake in another session,
/// which shares the folder, well clear.
const ABANDONED_AFTER: Duration = Duration::from_secs(5 * 60);

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

impl std::error::Error for LoadError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            LoadError::Missing => None,
            LoadError::Io(error) => Some(error),
            LoadError::Invalid(error) => Some(error),
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

impl std::error::Error for SaveError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            SaveError::Busy => None,
            SaveError::Io(error) => Some(error),
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
        let bytes = read_at_most_limit(file).map_err(LoadError::Io)?;
        SettingsDocument::parse(&bytes).map_err(LoadError::Invalid)
    }

    pub fn write(&self, document: &SettingsDocument) -> Result<(), SaveError> {
        let directory = self.path.parent().unwrap_or(Path::new("."));
        fs::create_dir_all(directory).map_err(SaveError::Io)?;
        remove_abandoned_temporaries(directory, SystemTime::now());
        let temporary = directory.join(temporary_name(std::process::id()));

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

fn temporary_name(pid: u32) -> String {
    format!("settings.json.{pid}.tmp")
}

/// Whether `name` is one of Awake's temporary files, `settings.json.<pid>.tmp`.
fn is_temporary_name(name: &OsStr) -> bool {
    name.to_str()
        .and_then(|name| name.strip_prefix("settings.json."))
        .and_then(|rest| rest.strip_suffix(".tmp"))
        .is_some_and(|pid| !pid.is_empty() && pid.bytes().all(|b| b.is_ascii_digit()))
}

/// Deletes temporary files left by an Awake that was killed between staging and renaming. Only
/// plain files with Awake's own name pattern that were last written [`ABANDONED_AFTER`] ago are
/// touched; a failure leaves the file for the next write.
fn remove_abandoned_temporaries(directory: &Path, now: SystemTime) {
    let Ok(entries) = fs::read_dir(directory) else {
        return;
    };
    for entry in entries.flatten() {
        if !is_temporary_name(&entry.file_name()) {
            continue;
        }
        // Directory entries describe the entry itself, never what a link points to.
        let Ok(metadata) = entry.metadata() else { continue };
        let abandoned = metadata.is_file()
            && metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT == 0
            && metadata.modified().is_ok_and(|modified| {
                now.duration_since(modified)
                    .is_ok_and(|age| age >= ABANDONED_AFTER)
            });
        if abandoned {
            let _ = fs::remove_file(entry.path());
        }
    }
}

/// Writes and flushes `path`; the flush is the one wait for the disk, see the module docs.
fn write_durably(path: &Path, bytes: &[u8]) -> io::Result<()> {
    // Always a new file: opening an existing name would write through any link planted there. A
    // leftover under this name (a reused process id) is removed first; removing a link removes
    // the link, not its target.
    let create = || OpenOptions::new().write(true).create_new(true).open(path);
    let mut file = match create() {
        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
            fs::remove_file(path)?;
            create()?
        }
        result => result?,
    };
    file.write_all(bytes)?;
    file.sync_all()
}

/// Refreshes the write time of `path` itself; a link planted at `path` gets the new time, not
/// the file it points to.
fn touch(path: &Path) -> io::Result<()> {
    let file: File = OpenOptions::new()
        .access_mode(FILE_WRITE_ATTRIBUTES)
        .share_mode(SHARE_ALL)
        .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)?;
    file.set_modified(SystemTime::now())
}

/// Reads another small PowerToys file, such as `language.json`, with the same size limit as
/// `settings.json`: a damaged or replaced file must not be loaded whole into memory.
pub fn read_bounded(path: &Path) -> io::Result<Vec<u8>> {
    let file = OpenOptions::new().read(true).share_mode(SHARE_ALL).open(path)?;
    let bytes = read_at_most_limit(file)?;
    if bytes.len() > MAX_FILE_BYTES {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            format!("{} is larger than {MAX_FILE_BYTES} bytes", path.display()),
        ));
    }
    Ok(bytes)
}

/// Reads up to one byte past [`MAX_FILE_BYTES`], which is how an oversized file is detected.
fn read_at_most_limit(file: File) -> io::Result<Vec<u8>> {
    let mut bytes = Vec::new();
    file.take(MAX_FILE_BYTES as u64 + 1).read_to_end(&mut bytes)?;
    Ok(bytes)
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
    fn other_powertoys_files_are_read_with_the_same_bound() {
        let dir = temp_dir("bounded");
        let small = dir.join("language.json");
        fs::write(&small, br#"{"language":"de-DE"}"#).unwrap();
        assert_eq!(read_bounded(&small).unwrap(), br#"{"language":"de-DE"}"#);
        let large = dir.join("log_settings.json");
        fs::write(&large, vec![b' '; MAX_FILE_BYTES + 1]).unwrap();
        assert_eq!(
            read_bounded(&large).unwrap_err().kind(),
            io::ErrorKind::InvalidData
        );
        assert_eq!(
            read_bounded(&dir.join("missing.json")).unwrap_err().kind(),
            io::ErrorKind::NotFound
        );
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn errors_expose_their_cause() {
        use std::error::Error;
        let io = || io::Error::from(io::ErrorKind::PermissionDenied);
        assert!(LoadError::Missing.source().is_none());
        assert!(LoadError::Io(io()).source().unwrap().is::<io::Error>());
        let invalid = LoadError::Invalid(SettingsError::TooLarge);
        assert!(invalid.source().unwrap().is::<SettingsError>());
        assert_eq!(invalid.to_string(), SettingsError::TooLarge.to_string());
        assert!(SaveError::Busy.source().is_none());
        let boxed: Box<dyn Error> = Box::new(SaveError::Io(io()));
        assert!(boxed.source().unwrap().is::<io::Error>());
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

    fn written_at(path: &Path, modified: SystemTime) {
        File::create(path).unwrap().set_modified(modified).unwrap();
    }

    #[test]
    fn abandoned_temporary_files_are_removed_and_nothing_else() {
        let dir = temp_dir("abandoned");
        let now = SystemTime::now();
        let old = now - ABANDONED_AFTER - Duration::from_secs(60);
        written_at(&dir.join("settings.json.99999.tmp"), old);
        // Another session's Awake may be writing this one right now.
        written_at(&dir.join("settings.json.12345.tmp"), now);
        for name in [
            "settings.json.abc.tmp",
            "settings.json..tmp",
            "other.json.1.tmp",
            "settings.json",
        ] {
            written_at(&dir.join(name), old);
        }
        fs::create_dir(dir.join("settings.json.4242.tmp")).unwrap();

        SettingsStore::new(dir.join("settings.json"))
            .write(&document())
            .unwrap();

        let mut left: Vec<_> = fs::read_dir(&dir)
            .unwrap()
            .flatten()
            .map(|e| e.file_name().into_string().unwrap())
            .collect();
        left.sort();
        assert_eq!(
            left,
            [
                "other.json.1.tmp",
                "settings.json",
                "settings.json..tmp",
                "settings.json.12345.tmp",
                "settings.json.4242.tmp",
                "settings.json.abc.tmp",
            ]
        );
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn a_leftover_under_our_own_temporary_name_is_replaced_not_reused() {
        let dir = temp_dir("leftover");
        let leftover = dir.join(temporary_name(std::process::id()));
        fs::write(&leftover, vec![b'x'; 64 * 1024]).unwrap();
        let store = SettingsStore::new(dir.join("settings.json"));
        store.write(&document()).unwrap();
        assert_eq!(store.read().unwrap(), document());
        assert!(!leftover.exists());
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn touch_does_not_follow_a_link() {
        let dir = temp_dir("touch-link");
        let target = dir.join("elsewhere.txt");
        let old = SystemTime::now() - Duration::from_secs(24 * 3600);
        written_at(&target, old);
        let link = dir.join("settings.json");
        // File symbolic links need Developer Mode or elevation; without either there is nothing
        // to plant, and so nothing to follow.
        if std::os::windows::fs::symlink_file(&target, &link).is_err() {
            fs::remove_dir_all(dir).ok();
            return;
        }
        touch(&link).unwrap();
        assert_eq!(fs::metadata(&target).unwrap().modified().unwrap(), old);
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
