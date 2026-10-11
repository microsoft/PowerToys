//! Every parser that sees text from people or from files other programs write must return,
//! whatever it is fed. Byte offsets inside multi-byte characters are the usual way a Rust
//! string parser panics, so the inputs mix valid forms, ASCII noise and wide characters.
//!
//! The generator is seeded, so a failure reproduces on every run and names its input.

use std::panic::{AssertUnwindSafe, catch_unwind};

use awake_core::cli;
use awake_core::duration::parse_duration;
use awake_core::powertoys::{language_tag, log_level};
use awake_core::settings::SettingsDocument;
use awake_core::strings::Strings;
use awake_core::time::{FixedZone, parse_time};

const ROUNDS: usize = 20_000;

/// Valid and nearly valid inputs to mutate, so the noise reaches the deeper branches.
const SEEDS: [&str; 14] = [
    "2026-10-10T09:15:32.1234567+02:00",
    "2026-10-10T09:15:32Z",
    "2026-10-10 18:00-0130",
    "2026-12-24",
    "17:30",
    "5:30 pm",
    "12am",
    "1h30m",
    "45m",
    "1d 2h 3m 4s",
    "de-DE",
    "zh-Hant-TW",
    "true",
    "-t:60",
];

/// Pieces that change byte lengths or sit on the parsers' decision points.
const PIECES: [&str; 32] = [
    "0", "1", "9", "-", "+", ":", ".", ",", " ", "T", "Z", "z", "h", "m", "s", "d", "am", "pm", "=", "é",
    "時", "😀", "１", "\u{301}", "\u{0}", "\u{FEFF}", "\t", "--", "for", "until", "-e", "--pid",
];

/// `SplitMix64`: small, seeded and good enough to spread inputs around.
struct Rng(u64);

impl Rng {
    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }

    fn below(&mut self, n: usize) -> usize {
        (self.next() % n as u64) as usize
    }

    fn pick<'a>(&mut self, items: &[&'a str]) -> &'a str {
        items[self.below(items.len())]
    }

    /// A seed with a few characters replaced, inserted or removed, or pure noise.
    fn text(&mut self) -> String {
        if self.below(4) == 0 {
            let len = self.below(12);
            return (0..len).map(|_| self.pick(&PIECES)).collect();
        }
        let mut chars: Vec<String> = self.pick(&SEEDS).chars().map(String::from).collect();
        for _ in 0..=self.below(3) {
            let at = self.below(chars.len() + 1);
            match self.below(3) {
                0 if at < chars.len() => self.pick(&PIECES).clone_into(&mut chars[at]),
                1 if at < chars.len() => {
                    chars.remove(at);
                }
                _ => chars.insert(at, self.pick(&PIECES).to_owned()),
            }
        }
        chars.concat()
    }

    /// Raw bytes, valid UTF-8 or not.
    fn bytes(&mut self) -> Vec<u8> {
        if self.below(2) == 0 {
            return self.text().into_bytes();
        }
        let len = self.below(24);
        (0..len).map(|_| self.next() as u8).collect()
    }
}

fn must_return<R>(what: &str, input: &dyn std::fmt::Debug, f: impl FnOnce() -> R) {
    assert!(
        catch_unwind(AssertUnwindSafe(f)).is_ok(),
        "{what} panicked on {input:?}"
    );
}

#[test]
fn time_and_duration_parsers_never_panic() {
    let mut rng = Rng(1);
    for _ in 0..ROUNDS {
        let text = rng.text();
        must_return("parse_time", &text, || parse_time(&text));
        must_return("parse_duration", &text, || parse_duration(&text));
        must_return("Strings::for_language", &text, || Strings::for_language(&text));
    }
}

/// Every short run of pieces after a date-time prefix, so each offset form and each byte length
/// of a trailing zone is tried, not just the ones the random walk happens to reach.
#[test]
fn every_short_suffix_after_a_date_time() {
    for prefix in [
        "2000-01-01T10:00",
        "2000-01-01T10:00+",
        "2000-01-01T10:00-",
        "2000-01-01T10:00:00.",
    ] {
        for a in PIECES {
            for b in PIECES {
                for c in PIECES {
                    let text = format!("{prefix}{a}{b}{c}");
                    must_return("parse_time", &text, || parse_time(&text));
                }
            }
        }
    }
}

#[test]
fn powertoys_files_never_panic() {
    let mut rng = Rng(2);
    for _ in 0..ROUNDS {
        let bytes = rng.bytes();
        must_return("language_tag", &bytes, || language_tag(&bytes));
        must_return("log_level", &bytes, || log_level(&bytes));
        let field = serde_json::to_string(&rng.text()).unwrap();
        let wrapped = format!(r#"{{"language":{field},"logLevel":{field}}}"#);
        must_return("language_tag", &wrapped, || language_tag(wrapped.as_bytes()));
        must_return("log_level", &wrapped, || log_level(wrapped.as_bytes()));
    }
}

#[test]
fn settings_files_never_panic() {
    let mut rng = Rng(3);
    for _ in 0..ROUNDS {
        let bytes = rng.bytes();
        must_return("SettingsDocument::parse", &bytes, || {
            if let Ok(document) = SettingsDocument::parse(&bytes) {
                let _ = document.settings(&FixedZone(0));
            }
        });
        // Arbitrary text where the parser reads it: the expiration and a preset label.
        let text = serde_json::to_string(&rng.text()).unwrap();
        let json = format!(
            r#"{{"properties":{{"mode":3,"expirationDateTime":{text},"customTrayTimes":{{{text}:60}}}}}}"#
        );
        must_return("SettingsDocument::settings", &json, || {
            let document = SettingsDocument::parse(json.as_bytes()).unwrap();
            document.settings(&FixedZone(-420))
        });
    }
}

#[test]
fn command_lines_never_panic() {
    let mut rng = Rng(4);
    for _ in 0..ROUNDS / 4 {
        let mut args = vec![cli::PROGRAM.to_owned()];
        for _ in 0..rng.below(5) {
            args.push(if rng.below(2) == 0 {
                rng.pick(&PIECES).to_owned()
            } else {
                rng.text()
            });
        }
        must_return("cli::parse", &args, || cli::parse(args.clone()));
    }
}
