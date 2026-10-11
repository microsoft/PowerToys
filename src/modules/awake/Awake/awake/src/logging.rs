//! File logging through the `log` facade.
//!
//! Lines are handed to a background writer through a bounded queue, so a slow or full disk
//! never stalls the thread that holds the keep-awake request: when the queue is full, lines are
//! dropped and counted instead. Each day's file is capped in size and old files are removed.
//!
//! Layout, as before: `Awake\Logs\<version>\Log_yyyy-MM-dd.log`.

use std::fs::{self, File, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::mpsc::{Receiver, SyncSender, TrySendError, sync_channel};
use std::sync::{Mutex, OnceLock};
use std::time::{Duration, Instant, SystemTime};

use awake_core::powertoys::LogLevel;
use awake_core::time::DateTimeOffset;
use log::{Level, LevelFilter, Log, Metadata, Record};

use crate::platform::clock::SystemClock;

const QUEUE_LINES: usize = 1024;
const MAX_FILE_BYTES: u64 = 4 * 1024 * 1024;
const RETENTION: Duration = Duration::from_secs(30 * 24 * 3600);

enum Message {
    Line(DateTimeOffset, String),
    Flush(SyncSender<()>),
}

struct FileLogger {
    sender: Mutex<Option<SyncSender<Message>>>,
    level: LevelFilter,
    dropped: AtomicU64,
}

static LOGGER: OnceLock<FileLogger> = OnceLock::new();
static EMERGENCY_PATH: OnceLock<PathBuf> = OnceLock::new();

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
        .spawn(move || write_lines(&root, &writer_dir, receiver))
        .is_err()
    {
        return;
    }
    let _ = EMERGENCY_PATH.set(directory.join("crash.log"));
    let logger = LOGGER.get_or_init(|| FileLogger {
        sender: Mutex::new(Some(sender)),
        level,
        dropped: AtomicU64::new(0),
    });
    if log::set_logger(logger).is_ok() {
        log::set_max_level(level);
    }
}

/// Waits up to `timeout` for queued lines to reach the disk.
pub fn flush(timeout: Duration) {
    let Some(logger) = LOGGER.get() else { return };
    let (done, wait) = sync_channel(1);
    let sent = logger
        .sender
        .lock()
        .ok()
        .and_then(|s| s.as_ref().map(|s| s.try_send(Message::Flush(done)).is_ok()));
    if sent == Some(true) {
        let _ = wait.recv_timeout(timeout);
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
        let Ok(guard) = self.sender.lock() else { return };
        if let Some(sender) = guard.as_ref()
            && let Err(TrySendError::Full(_)) = sender.try_send(Message::Line(SystemClock.now(), line))
        {
            self.dropped.fetch_add(1, Ordering::Relaxed);
        }
    }

    fn flush(&self) {}
}

fn write_lines(root: &Path, directory: &Path, receiver: Receiver<Message>) {
    remove_old_logs(root, directory);
    let mut sink = Sink {
        directory: directory.to_owned(),
        file: None,
        day: String::new(),
        written: 0,
        failures: 0,
    };
    while let Ok(message) = receiver.recv() {
        match message {
            Message::Line(at, line) => {
                if let Some(logger) = LOGGER.get() {
                    let dropped = logger.dropped.swap(0, Ordering::Relaxed);
                    if dropped > 0 {
                        sink.write(&at, &format!("[Warning] logging\n    {dropped} log lines were dropped because the disk was slow."));
                    }
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

/// Deletes log files and version folders older than the retention period.
fn remove_old_logs(root: &Path, current: &Path) {
    let cutoff = SystemTime::now()
        .checked_sub(RETENTION)
        .unwrap_or(SystemTime::UNIX_EPOCH);
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
            if started.elapsed() > Duration::from_secs(2) {
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

    #[test]
    fn sink_caps_file_size_and_survives_missing_directories() {
        let dir = std::env::temp_dir().join(format!("awake-log-test-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
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
}
