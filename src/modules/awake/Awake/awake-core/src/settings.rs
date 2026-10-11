//! `%LOCALAPPDATA%\Microsoft\PowerToys\Awake\settings.json`.
//!
//! Several programs write this file: the runner (for the Settings app), Command Palette, DSC and
//! Awake itself. Awake therefore edits the parsed document in place and only touches the keys it
//! owns, so unknown keys, key order and the order of the custom presets all survive.

use serde_json::{Map, Value};

use crate::mode::Mode;
use crate::time::{DateTimeOffset, LocalZone, ParsedTime, parse_time};

pub const MODULE_NAME: &str = "Awake";
pub const FILE_NAME: &str = "settings.json";
/// The `version` the runner stamps when it writes the file.
const SCAFFOLD_VERSION: &str = "1.0";
/// Larger files are not Awake settings; refusing them bounds memory use.
pub const MAX_FILE_BYTES: usize = 1024 * 1024;
/// More presets than this cannot be shown in a menu sensibly.
pub const MAX_PRESETS: usize = 64;

/// A "Keep awake on interval" entry: menu label and duration in seconds.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Preset {
    pub label: String,
    pub seconds: u32,
}

/// The settings file stores timed sessions in whole minutes, so a 90-second preset runs for two
/// minutes; rounding up never shortens what the user asked for.
pub fn round_up_to_minute(seconds: u32) -> u64 {
    u64::from(seconds).div_ceil(60) * 60
}

/// The `mode` value, kept even when it is not one Awake knows.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ModeSetting {
    Known(Mode),
    Unknown(i64),
}

/// The values Awake acts on, with the defaults System.Text.Json applies to missing keys.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct AwakeSettings {
    pub keep_display_on: bool,
    pub mode: ModeSetting,
    pub interval_hours: u32,
    pub interval_minutes: u32,
    /// `None` when missing, unreadable or a placeholder such as `0001-01-01`.
    pub expiration: Option<DateTimeOffset>,
    /// Empty means "use the built-in presets".
    pub custom_tray_times: Vec<Preset>,
}

impl AwakeSettings {
    pub fn timed_seconds(&self) -> u64 {
        u64::from(self.interval_hours) * 3600 + u64::from(self.interval_minutes) * 60
    }
}

impl Default for AwakeSettings {
    fn default() -> Self {
        AwakeSettings {
            keep_display_on: false,
            mode: ModeSetting::Known(Mode::Passive),
            interval_hours: 0,
            interval_minutes: 1,
            expiration: None,
            custom_tray_times: Vec::new(),
        }
    }
}

/// One field Awake writes back.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SettingsChange {
    Mode(Mode),
    KeepDisplayOn(bool),
    Interval { hours: u32, minutes: u32 },
    Expiration(DateTimeOffset),
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum SettingsError {
    TooLarge,
    NotJson(String),
    NotAnObject,
    InvalidField(&'static str),
}

impl std::fmt::Display for SettingsError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SettingsError::TooLarge => write!(f, "the file is larger than {MAX_FILE_BYTES} bytes"),
            SettingsError::NotJson(error) => write!(f, "the file is not valid JSON: {error}"),
            SettingsError::NotAnObject => f.write_str("the file is not a JSON object"),
            SettingsError::InvalidField(name) => write!(f, "\"{name}\" has the wrong type"),
        }
    }
}

impl std::error::Error for SettingsError {}

/// The whole settings file, parsed.
#[derive(Clone, Debug, PartialEq)]
pub struct SettingsDocument {
    root: Map<String, Value>,
}

impl SettingsDocument {
    /// Accepts a UTF-8 BOM and the stray NULs some writers leave behind, like `SettingsUtils` does.
    pub fn parse(bytes: &[u8]) -> Result<Self, SettingsError> {
        if bytes.len() > MAX_FILE_BYTES {
            return Err(SettingsError::TooLarge);
        }
        let bytes = bytes.strip_prefix(b"\xEF\xBB\xBF").unwrap_or(bytes);
        let bytes = trim_bytes(bytes, |b| b == 0 || b.is_ascii_whitespace());
        match serde_json::from_slice::<Value>(bytes) {
            Ok(Value::Object(root)) => Ok(SettingsDocument { root }),
            Ok(_) => Err(SettingsError::NotAnObject),
            Err(error) => Err(SettingsError::NotJson(error.to_string())),
        }
    }

    /// The file Awake creates when none exists, with the same keys and defaults as before.
    pub fn scaffold(now: DateTimeOffset) -> Self {
        let mut properties = Map::new();
        properties.insert("keepDisplayOn".into(), Value::Bool(false));
        properties.insert("mode".into(), Value::from(Mode::Passive.value()));
        properties.insert("intervalHours".into(), Value::from(0));
        properties.insert("intervalMinutes".into(), Value::from(1));
        properties.insert("expirationDateTime".into(), Value::String(now.to_json_string()));
        properties.insert("customTrayTimes".into(), Value::Object(Map::new()));
        let mut root = Map::new();
        root.insert("name".into(), Value::String(MODULE_NAME.into()));
        root.insert("version".into(), Value::String(SCAFFOLD_VERSION.into()));
        root.insert("properties".into(), Value::Object(properties));
        SettingsDocument { root }
    }

    /// Reads the values Awake acts on. Expirations without an offset are local time, as in .NET.
    pub fn settings(&self, zone: &dyn LocalZone) -> Result<AwakeSettings, SettingsError> {
        let mut settings = AwakeSettings::default();
        let properties = match self.root.get("properties") {
            None | Some(Value::Null) => return Ok(settings),
            Some(Value::Object(properties)) => properties,
            Some(_) => return Err(SettingsError::InvalidField("properties")),
        };

        if let Some(value) = present(properties, "keepDisplayOn") {
            settings.keep_display_on = value
                .as_bool()
                .ok_or(SettingsError::InvalidField("keepDisplayOn"))?;
        }
        if let Some(value) = present(properties, "mode") {
            let raw = value.as_i64().ok_or(SettingsError::InvalidField("mode"))?;
            settings.mode = Mode::from_value(raw).map_or(ModeSetting::Unknown(raw), ModeSetting::Known);
        }
        if let Some(value) = present(properties, "intervalHours") {
            settings.interval_hours = as_u32(value).ok_or(SettingsError::InvalidField("intervalHours"))?;
        }
        if let Some(value) = present(properties, "intervalMinutes") {
            settings.interval_minutes =
                as_u32(value).ok_or(SettingsError::InvalidField("intervalMinutes"))?;
        }
        settings.expiration = present(properties, "expirationDateTime")
            .and_then(Value::as_str)
            .and_then(parse_time)
            .and_then(|parsed| match parsed {
                ParsedTime::Absolute(at) => Some(at),
                ParsedTime::Local(local) => zone.resolve(local),
                ParsedTime::TimeOfDay { .. } => None,
            })
            .filter(|at| !at.is_placeholder());
        if let Some(Value::Object(presets)) = present(properties, "customTrayTimes") {
            settings.custom_tray_times = presets
                .iter()
                .filter_map(|(label, seconds)| {
                    let seconds = as_u32(seconds).filter(|s| *s > 0)?;
                    Some(Preset {
                        label: label.clone(),
                        seconds,
                    })
                })
                .take(MAX_PRESETS)
                .collect();
        }
        Ok(settings)
    }

    /// Applies `changes`; returns whether anything in the document actually changed.
    pub fn apply(&mut self, changes: &[SettingsChange]) -> bool {
        let properties = match self.root.get_mut("properties") {
            Some(Value::Object(properties)) => properties,
            _ => {
                self.root.insert("properties".into(), Value::Object(Map::new()));
                match self.root.get_mut("properties") {
                    Some(Value::Object(properties)) => properties,
                    _ => unreachable!("just inserted an object"),
                }
            }
        };
        let mut changed = false;
        let mut set = |key: &str, value: Value| {
            if properties.get(key) != Some(&value) {
                properties.insert(key.to_owned(), value);
                changed = true;
            }
        };
        for change in changes {
            match change {
                SettingsChange::Mode(mode) => set("mode", Value::from(mode.value())),
                SettingsChange::KeepDisplayOn(on) => set("keepDisplayOn", Value::Bool(*on)),
                SettingsChange::Interval { hours, minutes } => {
                    set("intervalHours", Value::from(*hours));
                    set("intervalMinutes", Value::from(*minutes));
                }
                SettingsChange::Expiration(at) => {
                    set("expirationDateTime", Value::String(at.to_json_string()))
                }
            }
        }
        changed
    }

    /// Compact JSON, like the other writers produce.
    pub fn to_bytes(&self) -> Vec<u8> {
        serde_json::to_vec(&self.root).unwrap_or_default()
    }
}

fn present<'a>(properties: &'a Map<String, Value>, key: &str) -> Option<&'a Value> {
    properties.get(key).filter(|value| !value.is_null())
}

fn as_u32(value: &Value) -> Option<u32> {
    value.as_u64().and_then(|n| u32::try_from(n).ok())
}

fn trim_bytes(bytes: &[u8], is_trim: impl Fn(u8) -> bool) -> &[u8] {
    let start = bytes.iter().position(|&b| !is_trim(b)).unwrap_or(bytes.len());
    let end = bytes.iter().rposition(|&b| !is_trim(b)).map_or(start, |i| i + 1);
    &bytes[start..end]
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::time::{FixedZone, Timestamp};

    const SAMPLE: &str = r#"{"name":"Awake","version":"1.0","properties":{"keepDisplayOn":true,"mode":2,"intervalHours":1,"intervalMinutes":30,"expirationDateTime":"2026-10-10T09:15:32.1234567+02:00","customTrayTimes":{"30 minutes":1800,"1 hour":3600,"2 hours":7200}},"unknownTopLevel":[1,2]}"#;

    fn now() -> DateTimeOffset {
        DateTimeOffset::new(Timestamp::from_ticks(134_000_000_000_000_000), -420)
    }

    #[test]
    fn reads_every_field() {
        let settings = SettingsDocument::parse(SAMPLE.as_bytes())
            .unwrap()
            .settings(&FixedZone(-420))
            .unwrap();
        assert!(settings.keep_display_on);
        assert_eq!(settings.mode, ModeSetting::Known(Mode::Timed));
        assert_eq!(settings.timed_seconds(), 5400);
        assert_eq!(settings.expiration.unwrap().offset_minutes, 120);
        let labels: Vec<_> = settings
            .custom_tray_times
            .iter()
            .map(|p| p.label.as_str())
            .collect();
        assert_eq!(
            labels,
            ["30 minutes", "1 hour", "2 hours"],
            "insertion order must be preserved"
        );
    }

    #[test]
    fn missing_keys_take_dotnet_defaults() {
        let settings = SettingsDocument::parse(br#"{"name":"Awake"}"#)
            .unwrap()
            .settings(&FixedZone(-420))
            .unwrap();
        assert_eq!(settings, AwakeSettings::default());
        assert_eq!(settings.interval_minutes, 1);
    }

    #[test]
    fn tolerates_bom_nuls_and_whitespace() {
        let mut bytes = b"\xEF\xBB\xBF \n".to_vec();
        bytes.extend_from_slice(SAMPLE.as_bytes());
        bytes.extend_from_slice(b"\0\0\r\n");
        assert!(SettingsDocument::parse(&bytes).is_ok());
    }

    #[test]
    fn rejects_files_that_are_not_settings() {
        assert!(matches!(
            SettingsDocument::parse(b""),
            Err(SettingsError::NotJson(_))
        ));
        assert!(matches!(
            SettingsDocument::parse(b"{\"name\":"),
            Err(SettingsError::NotJson(_))
        ));
        assert_eq!(SettingsDocument::parse(b"[1]"), Err(SettingsError::NotAnObject));
        assert_eq!(
            SettingsDocument::parse(&vec![b' '; MAX_FILE_BYTES + 1]),
            Err(SettingsError::TooLarge)
        );
    }

    #[test]
    fn wrong_types_are_errors_not_defaults() {
        for (body, field) in [
            (r#"{"properties":{"mode":"1"}}"#, "mode"),
            (r#"{"properties":{"keepDisplayOn":1}}"#, "keepDisplayOn"),
            (r#"{"properties":{"intervalHours":-1}}"#, "intervalHours"),
            (
                r#"{"properties":{"intervalMinutes":4294967296}}"#,
                "intervalMinutes",
            ),
            (r#"{"properties":5}"#, "properties"),
        ] {
            let error = SettingsDocument::parse(body.as_bytes())
                .unwrap()
                .settings(&FixedZone(-420))
                .unwrap_err();
            assert_eq!(error, SettingsError::InvalidField(field), "{body}");
        }
    }

    #[test]
    fn unknown_modes_are_kept_as_unknown() {
        let doc = SettingsDocument::parse(br#"{"properties":{"mode":7}}"#).unwrap();
        assert_eq!(
            doc.settings(&FixedZone(-420)).unwrap().mode,
            ModeSetting::Unknown(7)
        );
    }

    #[test]
    fn placeholder_and_garbage_expirations_mean_unset() {
        for value in [
            "\"0001-01-01T00:00:00+00:00\"",
            "\"0001-01-01T00:00:00\"",
            "\"tomorrow\"",
            "\"17:30\"",
            "42",
        ] {
            let body = format!(r#"{{"properties":{{"expirationDateTime":{value}}}}}"#);
            let settings = SettingsDocument::parse(body.as_bytes())
                .unwrap()
                .settings(&FixedZone(-420))
                .unwrap();
            assert_eq!(settings.expiration, None, "{value}");
        }
    }

    #[test]
    fn expirations_without_offset_are_local_time() {
        let doc = SettingsDocument::parse(br#"{"properties":{"expirationDateTime":"2026-10-10T10:00:00"}}"#)
            .unwrap();
        let at = doc.settings(&FixedZone(-420)).unwrap().expiration.unwrap();
        assert_eq!(at.to_json_string(), "2026-10-10T10:00:00-07:00");
    }

    #[test]
    fn invalid_presets_are_skipped_and_capped() {
        let mut presets: Vec<String> = (0..100).map(|i| format!("\"p{i}\":{}", i + 1)).collect();
        presets.push("\"bad\":-5".into());
        presets.push("\"zero\":0".into());
        let body = format!(
            r#"{{"properties":{{"customTrayTimes":{{{}}}}}}}"#,
            presets.join(",")
        );
        let settings = SettingsDocument::parse(body.as_bytes())
            .unwrap()
            .settings(&FixedZone(-420))
            .unwrap();
        assert_eq!(settings.custom_tray_times.len(), MAX_PRESETS);
        assert_eq!(
            settings.custom_tray_times[0],
            Preset {
                label: "p0".into(),
                seconds: 1
            }
        );
    }

    #[test]
    fn apply_only_touches_owned_keys() {
        let mut doc = SettingsDocument::parse(SAMPLE.as_bytes()).unwrap();
        assert!(doc.apply(&[SettingsChange::Mode(Mode::Passive)]));
        let text = String::from_utf8(doc.to_bytes()).unwrap();
        assert!(text.contains(r#""mode":0"#));
        assert!(text.contains(r#""unknownTopLevel":[1,2]"#));
        assert!(text.contains(r#""expirationDateTime":"2026-10-10T09:15:32.1234567+02:00""#));
        assert!(text.find("30 minutes").unwrap() < text.find("2 hours").unwrap());
        assert!(
            text.starts_with(r#"{"name":"Awake","version":"1.0","properties""#),
            "key order is preserved"
        );
    }

    #[test]
    fn apply_reports_no_change_for_identical_values() {
        let mut doc = SettingsDocument::parse(SAMPLE.as_bytes()).unwrap();
        let before = doc.to_bytes();
        let same = [
            SettingsChange::Mode(Mode::Timed),
            SettingsChange::KeepDisplayOn(true),
            SettingsChange::Interval {
                hours: 1,
                minutes: 30,
            },
        ];
        assert!(!doc.apply(&same));
        assert_eq!(doc.to_bytes(), before);
    }

    #[test]
    fn apply_creates_missing_properties() {
        let mut doc = SettingsDocument::parse(br#"{"name":"Awake"}"#).unwrap();
        assert!(doc.apply(&[SettingsChange::KeepDisplayOn(true)]));
        assert!(doc.settings(&FixedZone(-420)).unwrap().keep_display_on);
    }

    #[test]
    fn scaffold_round_trips_with_dotnet_defaults() {
        let doc = SettingsDocument::scaffold(now());
        let reparsed = SettingsDocument::parse(&doc.to_bytes()).unwrap();
        let settings = reparsed.settings(&FixedZone(-420)).unwrap();
        assert_eq!(settings.mode, ModeSetting::Known(Mode::Passive));
        assert_eq!((settings.interval_hours, settings.interval_minutes), (0, 1));
        assert_eq!(settings.expiration, Some(now()));
        let text = String::from_utf8(doc.to_bytes()).unwrap();
        assert!(text.starts_with(r#"{"name":"Awake","version":"1.0","properties":{"keepDisplayOn":false"#));
    }
}
