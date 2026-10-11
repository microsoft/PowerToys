//! Small PowerToys-wide files Awake reads but never writes.

use serde_json::Value;

/// `%LOCALAPPDATA%\Microsoft\PowerToys\language.json`: `{"language":"de-DE"}`. Empty means
/// "follow Windows".
pub fn language_tag(bytes: &[u8]) -> Option<String> {
    let value: Value = serde_json::from_slice(strip_bom(bytes)).ok()?;
    let tag = value.get("language")?.as_str()?.trim();
    (!tag.is_empty()).then(|| tag.to_owned())
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum LogLevel {
    Trace,
    Debug,
    Info,
    Warn,
    Error,
}

/// `%LOCALAPPDATA%\Microsoft\PowerToys\log_settings.json`: `{"logLevel":"trace"}`.
pub fn log_level(bytes: &[u8]) -> Option<LogLevel> {
    let value: Value = serde_json::from_slice(strip_bom(bytes)).ok()?;
    match value.get("logLevel")?.as_str()?.to_ascii_lowercase().as_str() {
        "trace" => Some(LogLevel::Trace),
        "debug" => Some(LogLevel::Debug),
        "info" => Some(LogLevel::Info),
        "warn" | "warning" => Some(LogLevel::Warn),
        "error" | "err" | "critical" => Some(LogLevel::Error),
        _ => None,
    }
}

fn strip_bom(bytes: &[u8]) -> &[u8] {
    bytes.strip_prefix(b"\xEF\xBB\xBF").unwrap_or(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn language() {
        assert_eq!(language_tag(br#"{"language":"de-DE"}"#), Some("de-DE".into()));
        assert_eq!(
            language_tag(b"\xEF\xBB\xBF{\"language\":\" ja-JP \"}"),
            Some("ja-JP".into())
        );
        assert_eq!(language_tag(br#"{"language":""}"#), None);
        assert_eq!(language_tag(b"not json"), None);
    }

    #[test]
    fn log_levels() {
        assert_eq!(log_level(br#"{"logLevel":"trace"}"#), Some(LogLevel::Trace));
        assert_eq!(log_level(br#"{"logLevel":"Warning"}"#), Some(LogLevel::Warn));
        assert_eq!(log_level(br#"{"logLevel":"loud"}"#), None);
        assert_eq!(log_level(b"{}"), None);
    }
}
