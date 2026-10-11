//! File logging through the `log` facade.
//!
//! Lines are handed to a background writer through a bounded queue, so a slow or full disk
//! never stalls the thread that holds the keep-awake request: when the queue is full, lines are
//! dropped and counted instead. Each day's file is capped in size and old files are removed.
//!
//! Old files are removed once the queue has gone quiet after the first line of each day, so the
//! lines of a short run, such as a refused start, reach the disk first and a process that runs
//! for weeks still keeps only the retention period. The cleanup yields to any new line. Awake may
//! run elevated while every folder under `%LOCALAPPDATA%` is the user's to change, so the cleanup
//! runs only when no folder from there down to the log folder is a link, holds those folders
//! open so none can be swapped for a link meanwhile, and skips linked version folders. If the
//! log folder is deleted while Awake runs, today's file is created again with the next line.
//!
//! Awake's code takes no lock on the log path: the queue's sender is shared directly (it is
//! `Sync`) and lines are sent with `try_send`, so a log call never blocks on a full queue. The
//! channel does hold an internal wake-up lock for a moment inside std when the writer is idle,
//! but that code cannot panic, so a panic raised in a log call cannot re-enter it and deadlock
//! the panic hook. Callers only read the UTC clock; the writer thread works out each line's
//! local time.
//!
//! Layout, as before: `Awake\Logs\<version>\Log_yyyy-MM-dd.log`.

use std::ffi::OsString;
use std::fs::{self, File, Metadata, OpenOptions};
use std::io::{self, Write};
use std::os::windows::ffi::OsStringExt;
use std::os::windows::fs::{MetadataExt, OpenOptionsExt};
use std::os::windows::io::AsRawHandle;
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::mpsc::{Receiver, RecvTimeoutError, SyncSender, TrySendError, sync_channel};
use std::time::{Duration, Instant, SystemTime};

use awake_core::powertoys::LogLevel;
use awake_core::time::{DateTimeOffset, Timestamp};
use log::{Level, LevelFilter, Log, Record};
use windows_sys::Win32::Storage::FileSystem::{
    FILE_ATTRIBUTE_DIRECTORY, FILE_ATTRIBUTE_REPARSE_POINT, FILE_FLAG_BACKUP_SEMANTICS,
    FILE_FLAG_OPEN_REPARSE_POINT, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    GetFinalPathNameByHandleW,
};

use crate::platform::clock::SystemClock;
use crate::platform::paths;

const QUEUE_LINES: usize = 1024;
const MAX_FILE_BYTES: u64 = 4 * 1024 * 1024;
const RETENTION: Duration = Duration::from_secs(30 * 24 * 3600);
/// How long cleaning up old logs may take before the writer gets on with today's lines.
const CLEANUP_BUDGET: Duration = Duration::from_secs(2);
/// How long the queue must stay empty before old logs are cleaned up.
const CLEANUP_AFTER_IDLE: Duration = Duration::from_secs(1);
/// The wait for `log::logger().flush()`; code that needs another bound calls `flush`.
const DEFAULT_FLUSH_TIMEOUT: Duration = Duration::from_millis(500);
/// Failed writes or opens in a row after which the day's file is given up on.
const MAX_FAILURES: u32 = 3;

enum Message {
    /// When the line was logged, in UTC; the writer adds the local offset.
    Line(Timestamp, String),
    Flush(SyncSender<()>),
}

struct FileLogger {
    sender: SyncSender<Message>,
    level: LevelFilter,
}

static LOGGER: OnceLock<FileLogger> = OnceLock::new();
static EMERGENCY_PATH: OnceLock<PathBuf> = OnceLock::new();
/// Lines dropped because the queue was full; the writer reports them with the next line.
static DROPPED: AtomicU64 = AtomicU64::new(0);

pub fn init(log_dir: &Path, version: &str, level: Option<LogLevel>) {
    let directory = log_dir.join(version);
    let level = match level {
        Some(LogLevel::Trace) => LevelFilter::Trace,
        Some(LogLevel::Debug) => LevelFilter::Debug,
        Some(LogLevel::Warn) => LevelFilter::Warn,
        Some(LogLevel::Error) => LevelFilter::Error,
        Some(LogLevel::Info) | None => LevelFilter::Info,
    };
    let (sender, receiver) = sync_channel(QUEUE_LINES);
    let writer_dir = directory.clone();
    let root = LogRoot {
        // Folders above `%LOCALAPPDATA%` belong to the profile, not to anything the user can
        // rearrange while signed in. Under the test hooks the data folder may be elsewhere.
        anchor: paths::local_app_data()
            .ok()
            .filter(|base| log_dir.starts_with(base))
            .or_else(|| log_dir.ancestors().nth(2).map(Path::to_owned))
            .unwrap_or_else(|| log_dir.to_owned()),
        path: log_dir.to_owned(),
    };
    if std::thread::Builder::new()
        .name("awake-log".into())
        .spawn(move || write_lines(&root, &writer_dir, &receiver, &DROPPED, CLEANUP_AFTER_IDLE))
        .is_err()
    {
        return;
    }
    let _ = EMERGENCY_PATH.set(directory.join("crash.log"));
    let logger = LOGGER.get_or_init(|| FileLogger { sender, level });
    if log::set_logger(logger).is_ok() {
        log::set_max_level(level);
    }
}

/// Waits up to `timeout` for queued lines to reach the disk.
pub fn flush(timeout: Duration) {
    if let Some(logger) = LOGGER.get() {
        logger.flush_within(timeout);
    }
}

/// Writes synchronously, bypassing the queue; for crashes, when the writer may never run again.
pub fn emergency(message: &str) {
    if let Some(path) = EMERGENCY_PATH.get()
        && let Some(parent) = path.parent()
        && fs::create_dir_all(parent).is_ok()
        && let Ok(mut file) = OpenOptions::new().create(true).append(true).open(path)
    {
        let _ = writeln!(file, "[{}] {message}", SystemClock.now().to_json_string());
    }
}

impl FileLogger {
    /// The one flush path, behind both `flush` and `Log::flush`. It waits at most `timeout`, and
    /// not at all when the queue is full or the writer is gone.
    fn flush_within(&self, timeout: Duration) {
        let (done, wait) = sync_channel(1);
        if self.sender.try_send(Message::Flush(done)).is_ok() {
            let _ = wait.recv_timeout(timeout);
        }
    }
}

impl Log for FileLogger {
    fn enabled(&self, metadata: &log::Metadata<'_>) -> bool {
        metadata.level() <= self.level
    }

    fn log(&self, record: &Record<'_>) {
        if !self.enabled(record.metadata()) {
            return;
        }
        let level = match record.level() {
            Level::Error => "Error",
            Level::Warn => "Warning",
            Level::Info => "Info",
            Level::Debug => "Debug",
            Level::Trace => "Trace",
        };
        let line = format!(
            "[{level}] {}::{}\n    {}",
            record.target(),
            record.line().unwrap_or(0),
            record.args()
        );
        let at = SystemClock.now_utc();
        if let Err(TrySendError::Full(_)) = self.sender.try_send(Message::Line(at, line)) {
            DROPPED.fetch_add(1, Ordering::Relaxed);
        }
    }

    fn flush(&self) {
        self.flush_within(DEFAULT_FLUSH_TIMEOUT);
    }
}

/// Writes queued lines until every sender is gone. Old logs are cleaned up once the queue has
/// been empty for `cleanup_after` after a new day's first line.
fn write_lines(
    root: &LogRoot,
    directory: &Path,
    receiver: &Receiver<Message>,
    dropped: &AtomicU64,
    cleanup_after: Duration,
) {
    let mut sink = Sink::new(directory);
    // A message that cut a cleanup short; it is handled before anything else.
    let mut next = None;
    loop {
        let message = if let Some(message) = next.take() {
            message
        } else if sink.cleanup_due {
            match receiver.recv_timeout(cleanup_after) {
                Ok(message) => message,
                Err(RecvTimeoutError::Timeout) => {
                    let outcome = remove_old_logs(root, directory, SystemTime::now(), CLEANUP_BUDGET, || {
                        next = receiver.try_recv().ok();
                        next.is_some()
                    });
                    // An interrupted cleanup runs again at the next quiet moment; one that
                    // finished or ran out of time waits for the next day.
                    sink.cleanup_due = outcome == Cleanup::Interrupted;
                    continue;
                }
                Err(RecvTimeoutError::Disconnected) => return,
            }
        } else {
            match receiver.recv() {
                Ok(message) => message,
                Err(_) => return,
            }
        };
        match message {
            Message::Line(utc, line) => {
                let at = DateTimeOffset::new(utc, SystemClock.offset_minutes_at(utc));
                let lost = dropped.swap(0, Ordering::Relaxed);
                if lost > 0 {
                    sink.write(
                        &at,
                        &format!(
                            "[Warning] logging\n    {lost} log lines were dropped because the disk was slow."
                        ),
                    );
                }
                sink.write(&at, &line);
            }
            Message::Flush(done) => {
                if let Some(file) = sink.file.as_mut() {
                    let _ = file.flush();
                }
                let _ = done.try_send(());
            }
        }
    }
}

struct Sink {
    directory: PathBuf,
    /// Today's file, once the first line has named it.
    path: PathBuf,
    file: Option<File>,
    day: String,
    written: u64,
    failures: u32,
    /// Set by each day's first line, so old files are removed even when Awake runs for weeks.
    cleanup_due: bool,
}

impl Sink {
    fn new(directory: &Path) -> Self {
        Sink {
            directory: directory.to_owned(),
            path: PathBuf::new(),
            file: None,
            day: String::new(),
            written: 0,
            failures: 0,
            cleanup_due: false,
        }
    }

    fn write(&mut self, at: &DateTimeOffset, line: &str) {
        let local = at.local();
        let day = format!("{:04}-{:02}-{:02}", local.year, local.month, local.day);
        if day != self.day {
            self.day = day;
            self.cleanup_due = true;
            self.failures = 0;
            self.open();
        } else if self.failures < MAX_FAILURES && !self.path.exists() {
            // The file is shared for deletion, so the module or Logs folder can be deleted while
            // Awake runs. Writes to a deleted file still succeed and are lost, so start again.
            self.open();
        }
        // After repeated failures (a full disk, or a folder that cannot be created) stop trying
        // until tomorrow's file.
        if self.failures >= MAX_FAILURES || self.written >= MAX_FILE_BYTES {
            return;
        }
        let Some(file) = self.file.as_mut() else { return };
        let text = format!(
            "[{:02}:{:02}:{:02}.{:07}] {line}\n",
            local.hour, local.minute, local.second, local.fraction
        );
        let limit_reached = self.written + text.len() as u64 >= MAX_FILE_BYTES;
        let text = if limit_reached {
            "[Warning] logging\n    This log reached its size limit; later lines go to tomorrow's file.\n"
                .to_owned()
        } else {
            text
        };
        match file.write_all(text.as_bytes()) {
            Ok(()) => {
                self.written = if limit_reached {
                    MAX_FILE_BYTES
                } else {
                    self.written + text.len() as u64
                };
                self.failures = 0;
            }
            Err(_) => self.failures += 1,
        }
    }

    /// A failed open counts as a failed write, so a folder that cannot be created is not tried
    /// again for every line.
    fn open(&mut self) {
        self.file = None;
        self.written = 0;
        self.path = self.directory.join(format!("Log_{}.log", self.day));
        if fs::create_dir_all(&self.directory).is_ok() {
            self.file = OpenOptions::new().create(true).append(true).open(&self.path).ok();
        }
        match self.file.as_ref() {
            Some(file) => {
                self.written = file.metadata().map_or(0, |m| m.len());
                self.failures = 0;
            }
            None => self.failures += 1,
        }
    }
}

#[derive(Debug, PartialEq, Eq)]
enum Cleanup {
    Done,
    OutOfTime,
    Interrupted,
}

/// The folder old logs are cleaned from, and the folder above it from which no folder on the way
/// down may be a link.
struct LogRoot {
    anchor: PathBuf,
    path: PathBuf,
}

/// A real folder, not a junction or symbolic link to one.
fn is_plain_directory(metadata: &Metadata) -> bool {
    let attributes = metadata.file_attributes();
    attributes & FILE_ATTRIBUTE_DIRECTORY != 0 && attributes & FILE_ATTRIBUTE_REPARSE_POINT == 0
}

/// Opens a directory, or with `follow_links` false the link itself, and holds it: without delete
/// sharing, neither it nor any folder above it can be renamed or deleted, and so none can be
/// replaced by a link, until the handle is closed.
fn hold_directory(path: &Path, follow_links: bool) -> io::Result<File> {
    let flags = if follow_links {
        FILE_FLAG_BACKUP_SEMANTICS
    } else {
        FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT
    };
    OpenOptions::new()
        .read(true)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(flags)
        .open(path)
}

/// Where an open file or folder really is, with every link resolved.
fn final_path(file: &File) -> io::Result<PathBuf> {
    let mut buffer = vec![0u16; 260];
    loop {
        let capacity = u32::try_from(buffer.len()).unwrap_or(u32::MAX);
        // SAFETY: the handle is open and `buffer` holds `capacity` characters.
        let length =
            unsafe { GetFinalPathNameByHandleW(file.as_raw_handle(), buffer.as_mut_ptr(), capacity, 0) };
        match length as usize {
            0 => return Err(io::Error::last_os_error()),
            // On success the length leaves out the NUL; when too small, it is the size needed.
            written if written < buffer.len() => {
                buffer.truncate(written);
                return Ok(PathBuf::from(OsString::from_wide(&buffer)));
            }
            needed => buffer.resize(needed, 0),
        }
    }
}

/// Holds the log root open and returns its resolved path, if resolving it leads exactly where
/// resolving the anchor and walking down by name does, which means no folder in between is a
/// link.
fn confined_root(root: &LogRoot) -> Option<(File, PathBuf)> {
    let below = root.path.strip_prefix(&root.anchor).ok()?;
    let held = hold_directory(&root.path, true).ok()?;
    let actual = final_path(&held).ok()?;
    // The anchor is only looked up, so it is opened with full sharing.
    let anchor = OpenOptions::new()
        .read(true)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(&root.anchor)
        .ok()?;
    let expected = final_path(&anchor).ok()?.join(below);
    // File names compare without regard to case, and `below` is spelled as the caller wrote it.
    if actual.to_string_lossy().to_lowercase() != expected.to_string_lossy().to_lowercase() {
        log::warn!(
            "Not cleaning up old logs: {} leads to {}.",
            root.path.display(),
            actual.display()
        );
        return None;
    }
    Some((held, actual))
}

/// Deletes `.log` files last written before the retention period, then any version folder
/// other than `current` that this leaves empty. Stops once `budget` has been spent, so a huge
/// folder cannot keep today's lines waiting, and as soon as `interrupted` says a line is waiting.
/// Linked folders are never entered: through one, an elevated Awake would delete files elsewhere.
fn remove_old_logs(
    root: &LogRoot,
    current: &Path,
    now: SystemTime,
    budget: Duration,
    mut interrupted: impl FnMut() -> bool,
) -> Cleanup {
    let cutoff = now.checked_sub(RETENTION).unwrap_or(SystemTime::UNIX_EPOCH);
    let started = Instant::now();
    // Held until the cleanup ends, so no folder from the anchor down can change under it.
    let Some((_held, resolved)) = confined_root(root) else {
        return Cleanup::Done;
    };
    let Ok(versions) = fs::read_dir(&resolved) else {
        return Cleanup::Done;
    };
    for entry in versions.flatten() {
        let version = entry.path();
        // Checked through a handle on the entry itself, held while its files are listed, so the
        // folder listed is the one checked.
        let Ok(held) = hold_directory(&version, false) else {
            continue;
        };
        if !held.metadata().is_ok_and(|m| is_plain_directory(&m)) {
            continue;
        }
        let mut remaining = 0;
        for file in fs::read_dir(&version).into_iter().flatten().flatten() {
            if interrupted() {
                return Cleanup::Interrupted;
            }
            if started.elapsed() > budget {
                return Cleanup::OutOfTime;
            }
            let old = file
                .metadata()
                .and_then(|m| m.modified())
                .is_ok_and(|modified| modified < cutoff);
            if old && file.path().extension().is_some_and(|e| e == "log") {
                let _ = fs::remove_file(file.path());
            } else {
                remaining += 1;
            }
        }
        // The folder cannot be removed while held. Should it become a link in between,
        // removing it removes only the link.
        drop(held);
        if remaining == 0 && current.file_name() != Some(entry.file_name().as_os_str()) {
            let _ = fs::remove_dir(&version);
        }
    }
    Cleanup::Done
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_dir(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("awake-log-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    /// A log root whose only unchecked folder is its parent.
    fn log_root(path: &Path) -> LogRoot {
        LogRoot {
            anchor: path.parent().unwrap().to_owned(),
            path: path.to_owned(),
        }
    }

    #[test]
    fn a_folder_that_cannot_be_created_is_not_tried_again_for_every_line() {
        let dir = temp_dir("uncreatable");
        // A file where the version folder's parent should be makes the folder impossible.
        let blocker = dir.join("Logs");
        fs::write(&blocker, "").unwrap();
        let directory = blocker.join("v1");
        let mut sink = Sink::new(&directory);
        let now = SystemClock.now();
        for _ in 0..MAX_FAILURES {
            sink.write(&now, "lost");
        }
        assert_eq!(sink.failures, MAX_FAILURES);

        // Once the folder could be created, the same day still does not try again.
        fs::remove_file(&blocker).unwrap();
        sink.write(&now, "still lost");
        assert!(!directory.exists());

        // The next day's file gets a fresh start.
        let tomorrow = now.saturating_add_secs(24 * 3600);
        sink.write(&tomorrow, "tomorrow");
        assert!(fs::read_to_string(&sink.path).unwrap().contains("tomorrow"));
        fs::remove_dir_all(dir).ok();
    }

    #[test]
    fn sink_caps_file_size_and_survives_missing_directories() {
        let dir = temp_dir("sink");
        let mut sink = Sink::new(&dir.join("v1"));
        let now = SystemClock.now();
        let line = "x".repeat(64 * 1024);
        for _ in 0..100 {
            sink.write(&now, &line);
        }
        let file = fs::read_dir(dir.join("v1"))
            .unwrap()
            .flatten()
            .next()
            .unwrap()
            .path();
        let size = fs::metadata(&file).unwrap().len();
        assert!(size <= MAX_FILE_BYTES + 1024, "{size}");
        assert!(fs::read_to_string(&file).unwrap().contains("size limit"));
        fs::remove_dir_all(dir).ok();
    }

    fn file_written_at(path: &Path, modified: SystemTime) {
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        let file = File::create(path).unwrap();
        file.set_modified(modified).unwrap();
    }

    #[test]
    fn old_logs_and_emptied_version_folders_are_removed() {
        let root = temp_dir("retention");
        let now = SystemTime::now();
        let old = now - RETENTION - Duration::from_secs(3600);
        let recent = now - Duration::from_secs(3600);
        let current = root.join("v2");

        file_written_at(&root.join("v1").join("Log_old.log"), old);
        file_written_at(&root.join("v1").join("crash.log"), old);
        file_written_at(&root.join("v1b").join("Log_old.log"), old);
        file_written_at(&root.join("v1b").join("notes.txt"), old);
        file_written_at(&root.join("v1c").join("Log_recent.log"), recent);
        file_written_at(&current.join("Log_old.log"), old);
        file_written_at(&current.join("Log_today.log"), now);
        fs::create_dir_all(root.join("empty")).unwrap();
        file_written_at(&root.join("Log_at_root.log"), old);

        assert_eq!(
            remove_old_logs(&log_root(&root), &current, now, Duration::from_secs(60), || false),
            Cleanup::Done
        );

        assert!(!root.join("v1").exists(), "a folder of old logs is removed");
        assert!(!root.join("v1b").join("Log_old.log").exists());
        assert!(
            root.join("v1b").join("notes.txt").exists(),
            "only .log files are deleted"
        );
        assert!(
            root.join("v1c").join("Log_recent.log").exists(),
            "recent logs are kept"
        );
        assert!(!current.join("Log_old.log").exists());
        assert!(current.join("Log_today.log").exists());
        assert!(!root.join("empty").exists(), "empty version folders are removed");
        assert!(
            root.join("Log_at_root.log").exists(),
            "only version folders are cleaned"
        );

        // The current folder survives even when nothing is left in it.
        fs::remove_file(current.join("Log_today.log")).unwrap();
        remove_old_logs(&log_root(&root), &current, now, Duration::from_secs(60), || false);
        assert!(current.is_dir());
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn cleanup_without_a_log_folder_does_nothing() {
        let root = std::env::temp_dir().join(format!("awake-log-none-{}", std::process::id()));
        let _ = fs::remove_dir_all(&root);
        let outcome = remove_old_logs(
            &log_root(&root),
            &root.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || false,
        );
        assert_eq!(outcome, Cleanup::Done);
        assert!(!root.exists());
    }

    /// `mklink /J` needs no privilege, unlike a symbolic link.
    fn junction(link: &Path, target: &Path) {
        let status = std::process::Command::new("cmd")
            .arg("/C")
            .arg("mklink")
            .arg("/J")
            .arg(link)
            .arg(target)
            .stdout(std::process::Stdio::null())
            .status()
            .unwrap();
        assert!(status.success());
    }

    #[test]
    fn cleanup_does_not_follow_junctions() {
        let root = temp_dir("junction");
        let elsewhere = temp_dir("junction-target");
        let old = SystemTime::now() - RETENTION - Duration::from_secs(3600);
        file_written_at(&elsewhere.join("v0").join("Log_old.log"), old);
        file_written_at(&elsewhere.join("Log_old.log"), old);
        fs::create_dir_all(&root).unwrap();
        junction(&root.join("v0"), &elsewhere);

        remove_old_logs(
            &log_root(&root),
            &root.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || false,
        );
        assert!(
            elsewhere.join("Log_old.log").exists(),
            "a linked version folder is skipped"
        );
        assert!(root.join("v0").exists(), "and left in place");

        // A log root that is itself a link is left alone entirely.
        let linked_root = root.join("linked-root");
        junction(&linked_root, &elsewhere);
        remove_old_logs(
            &log_root(&linked_root),
            &linked_root.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || false,
        );
        assert!(elsewhere.join("v0").join("Log_old.log").exists());

        // Removing a junction removes the link only.
        fs::remove_dir(root.join("v0")).unwrap();
        fs::remove_dir(&linked_root).unwrap();
        fs::remove_dir_all(root).ok();
        fs::remove_dir_all(elsewhere).ok();
    }

    #[test]
    fn cleanup_does_not_run_through_a_linked_folder_above_the_log_folder() {
        let base = temp_dir("linked-parent");
        let elsewhere = temp_dir("linked-parent-target");
        let old = SystemTime::now() - RETENTION - Duration::from_secs(3600);
        // A junction in place of a folder between the anchor and the log folder, leading to a
        // real `Logs` folder somewhere else, as `C:\Windows\Logs` would be.
        let protected = elsewhere.join("Logs").join("CBS").join("Log_old.log");
        file_written_at(&protected, old);
        junction(&base.join("Awake"), &elsewhere);
        let through_link = LogRoot {
            anchor: base.clone(),
            path: base.join("Awake").join("Logs"),
        };
        assert!(through_link.path.is_dir(), "the log folder resolves");
        remove_old_logs(
            &through_link,
            &through_link.path.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || false,
        );
        assert!(protected.exists(), "nothing is deleted through the link");

        // The same layout with real folders is cleaned up.
        let real = base.join("Real").join("Logs");
        let stale = real.join("v0").join("Log_old.log");
        file_written_at(&stale, old);
        let direct = LogRoot {
            anchor: base.clone(),
            path: real.clone(),
        };
        remove_old_logs(
            &direct,
            &real.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || false,
        );
        assert!(!stale.exists());

        fs::remove_dir(base.join("Awake")).unwrap();
        fs::remove_dir_all(base).ok();
        fs::remove_dir_all(elsewhere).ok();
    }

    #[test]
    fn a_held_folder_keeps_every_folder_above_it_in_place() {
        let base = temp_dir("held");
        let inner = base.join("a").join("b");
        fs::create_dir_all(&inner).unwrap();
        let held = hold_directory(&inner, true).unwrap();
        assert!(fs::rename(base.join("a"), base.join("moved")).is_err());
        assert!(fs::rename(&inner, base.join("a").join("moved")).is_err());
        drop(held);
        fs::rename(base.join("a"), base.join("moved")).unwrap();
        fs::remove_dir_all(base).ok();
    }

    #[test]
    fn cleanup_stops_as_soon_as_a_line_is_waiting() {
        let root = temp_dir("interrupted");
        let old = SystemTime::now() - RETENTION - Duration::from_secs(3600);
        file_written_at(&root.join("v0").join("Log_a.log"), old);
        file_written_at(&root.join("v0").join("Log_b.log"), old);
        let outcome = remove_old_logs(
            &log_root(&root),
            &root.join("v1"),
            SystemTime::now(),
            CLEANUP_BUDGET,
            || true,
        );
        assert_eq!(outcome, Cleanup::Interrupted);
        assert!(root.join("v0").join("Log_a.log").exists() && root.join("v0").join("Log_b.log").exists());
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn a_deleted_log_folder_is_created_again_by_the_next_line() {
        let root = temp_dir("deleted");
        let directory = root.join("Logs").join("v1");
        let mut sink = Sink::new(&directory);
        let now = SystemClock.now();
        sink.write(&now, "before");
        // The way a user or uninstaller removes the module folder while Awake runs.
        fs::remove_dir_all(root.join("Logs")).unwrap();
        sink.write(&now, "after");
        let text = fs::read_to_string(&sink.path).unwrap();
        assert!(text.contains("after") && !text.contains("before"), "{text}");

        // Deleting only today's file works the same way.
        fs::remove_file(&sink.path).unwrap();
        sink.write(&now, "again");
        assert!(fs::read_to_string(&sink.path).unwrap().contains("again"));
        fs::remove_dir_all(root).ok();
    }

    fn line_at(sender: &SyncSender<Message>, at: Timestamp, text: &str) {
        sender.send(Message::Line(at, text.to_owned())).unwrap();
        let (done, wait) = sync_channel(1);
        sender.send(Message::Flush(done)).unwrap();
        wait.recv_timeout(Duration::from_secs(30)).unwrap();
    }

    fn eventually(condition: impl Fn() -> bool) -> bool {
        let started = Instant::now();
        while started.elapsed() < Duration::from_secs(30) {
            if condition() {
                return true;
            }
            std::thread::sleep(Duration::from_millis(20));
        }
        false
    }

    #[test]
    fn lines_reach_the_disk_before_old_logs_are_cleaned_up() {
        let root = temp_dir("first-lines");
        let directory = root.join("v1");
        let stale = root.join("v0").join("Log_old.log");
        file_written_at(&stale, SystemTime::now() - RETENTION - Duration::from_secs(3600));
        let (sender, receiver) = sync_channel(16);
        let writer = {
            let (root, directory) = (root.clone(), directory.clone());
            std::thread::spawn(move || {
                static NONE_DROPPED: AtomicU64 = AtomicU64::new(0);
                write_lines(
                    &log_root(&root),
                    &directory,
                    &receiver,
                    &NONE_DROPPED,
                    Duration::from_secs(600),
                );
            })
        };
        line_at(&sender, SystemClock.now_utc(), "Not starting");
        let text = fs::read_dir(&directory)
            .unwrap()
            .flatten()
            .map(|e| fs::read_to_string(e.path()).unwrap())
            .collect::<String>();
        assert!(text.contains("Not starting"), "{text}");
        assert!(stale.exists(), "the cleanup waits for a quiet queue");
        drop(sender);
        writer.join().unwrap();
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn old_logs_are_cleaned_up_again_when_the_day_changes() {
        let root = temp_dir("rollover");
        let directory = root.join("v1");
        let old = SystemTime::now() - RETENTION - Duration::from_secs(3600);
        let stale = root.join("v0").join("Log_old.log");
        file_written_at(&stale, old);
        let (sender, receiver) = sync_channel(16);
        let writer = {
            let (root, directory) = (root.clone(), directory.clone());
            std::thread::spawn(move || {
                static NONE_DROPPED: AtomicU64 = AtomicU64::new(0);
                write_lines(
                    &log_root(&root),
                    &directory,
                    &receiver,
                    &NONE_DROPPED,
                    Duration::from_millis(20),
                );
            })
        };
        let today = SystemClock.now_utc();
        line_at(&sender, today, "first day");
        assert!(eventually(|| !stale.exists()), "the first day's cleanup runs");

        // Files that age past the retention period later in the day wait for the next day.
        file_written_at(&stale, old);
        line_at(&sender, today, "same day");
        std::thread::sleep(Duration::from_millis(300));
        assert!(stale.exists(), "one cleanup per day");

        line_at(&sender, today.saturating_add_secs(24 * 3600), "next day");
        assert!(eventually(|| !stale.exists()), "the next day's cleanup runs");
        drop(sender);
        writer.join().unwrap();
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn writer_flushes_and_reports_dropped_lines() {
        static WRITER_DROPPED: AtomicU64 = AtomicU64::new(0);
        let root = temp_dir("writer");
        let directory = root.join("v1");
        let (sender, receiver) = sync_channel(4);
        let writer = {
            let (root, directory) = (root.clone(), directory.clone());
            std::thread::spawn(move || {
                write_lines(
                    &log_root(&root),
                    &directory,
                    &receiver,
                    &WRITER_DROPPED,
                    CLEANUP_AFTER_IDLE,
                );
            })
        };
        let logger = FileLogger {
            sender,
            level: LevelFilter::Info,
        };
        WRITER_DROPPED.store(3, Ordering::Relaxed);
        logger.log(
            &Record::builder()
                .level(Level::Info)
                .target("test")
                .args(format_args!("hello"))
                .build(),
        );
        // Through the facade's method, which must reach the writer like `flush` does.
        Log::flush(&logger);
        let text = fs::read_dir(&directory)
            .unwrap()
            .flatten()
            .map(|e| fs::read_to_string(e.path()).unwrap())
            .collect::<String>();
        assert!(text.contains("3 log lines were dropped"), "{text}");
        assert!(text.contains("hello"), "{text}");
        assert_eq!(WRITER_DROPPED.load(Ordering::Relaxed), 0);

        // Dropping the only sender ends the writer.
        drop(logger);
        writer.join().unwrap();
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn flushing_a_full_queue_does_not_wait() {
        let (sender, _receiver) = sync_channel(0);
        let logger = FileLogger {
            sender,
            level: LevelFilter::Info,
        };
        let started = Instant::now();
        logger.flush_within(Duration::from_secs(30));
        assert!(started.elapsed() < Duration::from_secs(5));
    }
}
