//! File logging through the `log` facade.
//!
//! Lines are handed to a background writer through a bounded queue, so a slow or full disk
//! never stalls the thread that holds the keep-awake request: when the queue is full, lines are
//! dropped and counted instead. Each day's file is capped in size and old files are removed.
//!
//! Logging takes no locks. The queue's sender is shared directly (it is `Sync`), so a log call,
//! including one from the panic hook, never waits on another thread or on itself. Callers only
//! read the UTC clock; the writer thread works out each line's local time.
//!
//! Layout, as before: `Awake\Logs\<version>\Log_yyyy-MM-dd.log`.

use std::fs::{self, File, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::mpsc::{Receiver, SyncSender, TrySendError, sync_channel};
use std::time::{Duration, Instant, SystemTime};

use awake_core::powertoys::LogLevel;
use awake_core::time::{DateTimeOffset, Timestamp};
use log::{Level, LevelFilter, Log, Metadata, Record};

use crate::platform::clock::SystemClock;

const QUEUE_LINES: usize = 1024;
const MAX_FILE_BYTES: u64 = 4 * 1024 * 1024;
const RETENTION: Duration = Duration::from_secs(30 * 24 * 3600);
/// How long cleaning up old logs may take before the writer gets on with today's lines.
const CLEANUP_BUDGET: Duration = Duration::from_secs(2);
/// The wait for `log::logger().flush()`; code that needs another bound calls `flush`.
const DEFAULT_FLUSH_TIMEOUT: Duration = Duration::from_millis(500);

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
    let root = log_dir.to_owned();
    if std::thread::Builder::new()
        .name("awake-log".into())
        .spawn(move || write_lines(&root, &writer_dir, &receiver, &DROPPED))
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
    fn enabled(&self, metadata: &Metadata<'_>) -> bool {
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

fn write_lines(root: &Path, directory: &Path, receiver: &Receiver<Message>, dropped: &AtomicU64) {
    remove_old_logs(root, directory, SystemTime::now(), CLEANUP_BUDGET);
    let mut sink = Sink {
        directory: directory.to_owned(),
        file: None,
        day: String::new(),
        written: 0,
        failures: 0,
    };
    while let Ok(message) = receiver.recv() {
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
    file: Option<File>,
    day: String,
    written: u64,
    failures: u32,
}

impl Sink {
    fn write(&mut self, at: &DateTimeOffset, line: &str) {
        let local = at.local();
        let day = format!("{:04}-{:02}-{:02}", local.year, local.month, local.day);
        if day != self.day {
            self.day = day;
            self.file = None;
            self.failures = 0;
            let path = self.directory.join(format!("Log_{}.log", self.day));
            if fs::create_dir_all(&self.directory).is_ok() {
                self.file = OpenOptions::new().create(true).append(true).open(&path).ok();
                self.written = self
                    .file
                    .as_ref()
                    .and_then(|f| f.metadata().ok())
                    .map_or(0, |m| m.len());
            }
        }
        // After repeated failures (a full disk, usually) stop trying until tomorrow's file.
        if self.failures >= 3 || self.written >= MAX_FILE_BYTES {
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
}

/// Deletes `.log` files last written before the retention period, then any version folder
/// other than `current` that this leaves empty. Stops once `budget` has been spent, so a huge
/// folder cannot keep today's lines waiting.
fn remove_old_logs(root: &Path, current: &Path, now: SystemTime, budget: Duration) {
    let cutoff = now.checked_sub(RETENTION).unwrap_or(SystemTime::UNIX_EPOCH);
    let started = Instant::now();
    let Ok(versions) = fs::read_dir(root) else { return };
    for version in versions.flatten().map(|e| e.path()).filter(|p| p.is_dir()) {
        let mut remaining = 0;
        for file in fs::read_dir(&version).into_iter().flatten().flatten() {
            let old = file
                .metadata()
                .and_then(|m| m.modified())
                .is_ok_and(|modified| modified < cutoff);
            if old && file.path().extension().is_some_and(|e| e == "log") {
                let _ = fs::remove_file(file.path());
            } else {
                remaining += 1;
            }
            if started.elapsed() > budget {
                return;
            }
        }
        if remaining == 0 && version != current {
            let _ = fs::remove_dir(&version);
        }
    }
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

    #[test]
    fn sink_caps_file_size_and_survives_missing_directories() {
        let dir = temp_dir("sink");
        let mut sink = Sink {
            directory: dir.join("v1"),
            file: None,
            day: String::new(),
            written: 0,
            failures: 0,
        };
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

        remove_old_logs(&root, &current, now, Duration::from_secs(60));

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
        remove_old_logs(&root, &current, now, Duration::from_secs(60));
        assert!(current.is_dir());
        fs::remove_dir_all(root).ok();
    }

    #[test]
    fn cleanup_without_a_log_folder_does_nothing() {
        let root = std::env::temp_dir().join(format!("awake-log-none-{}", std::process::id()));
        let _ = fs::remove_dir_all(&root);
        remove_old_logs(&root, &root.join("v1"), SystemTime::now(), CLEANUP_BUDGET);
        assert!(!root.exists());
    }

    #[test]
    fn writer_flushes_and_reports_dropped_lines() {
        let root = temp_dir("writer");
        let directory = root.join("v1");
        let (sender, receiver) = sync_channel(4);
        static WRITER_DROPPED: AtomicU64 = AtomicU64::new(0);
        let writer = {
            let (root, directory) = (root.clone(), directory.clone());
            std::thread::spawn(move || write_lines(&root, &directory, &receiver, &WRITER_DROPPED))
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
