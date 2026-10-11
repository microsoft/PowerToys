//! Turns `Resources.resx` and its localized copies into a Rust string table.
//!
//! The resx files stay the source of truth so the PowerToys localization pipeline keeps
//! translating them. Localized files arrive either as `<lang>/Resources.resx` (Win32 layout)
//! or as `Resources.<lang>.resx`.

use std::collections::BTreeMap;
use std::fmt::Write as _;
use std::path::{Path, PathBuf};
use std::{env, fs};

fn main() {
    let strings_dir = PathBuf::from(env::var("CARGO_MANIFEST_DIR").unwrap()).join("..");
    let neutral_path = strings_dir.join("Resources.resx");
    println!("cargo:rerun-if-changed={}", neutral_path.display());

    let neutral = parse_resx(&neutral_path);
    let localized = find_localized(&strings_dir);

    let mut out = String::new();
    out.push_str("/// Identifies a localizable string from `Resources.resx`.\n");
    out.push_str("#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]\npub enum StringId {\n");
    for (index, (name, _)) in neutral.iter().enumerate() {
        let _ = writeln!(out, "    {} = {index},", variant_name(name));
    }
    out.push_str("}\n\n");
    let _ = writeln!(out, "pub(crate) const COUNT: usize = {};\n", neutral.len());

    out.push_str("pub(crate) static NEUTRAL: [&str; COUNT] = [\n");
    for (_, value) in &neutral {
        let _ = writeln!(out, "    {value:?},");
    }
    out.push_str("];\n\n");

    out.push_str("pub(crate) static LOCALIZED: &[(&str, [Option<&str>; COUNT])] = &[\n");
    for (language, path) in &localized {
        println!("cargo:rerun-if-changed={}", path.display());
        let table: BTreeMap<String, String> = parse_resx(path).into_iter().collect();
        let _ = writeln!(out, "    ({language:?}, [");
        for (name, _) in &neutral {
            match table.get(name) {
                Some(value) => _ = writeln!(out, "        Some({value:?}),"),
                None => out.push_str("        None,\n"),
            }
        }
        out.push_str("    ]),\n");
    }
    out.push_str("];\n");

    fs::write(
        PathBuf::from(env::var("OUT_DIR").unwrap()).join("strings.rs"),
        out,
    )
    .unwrap();
}

/// `AWAKE_CLI_ERR_PAST` becomes `CliErrPast`.
fn variant_name(resource_name: &str) -> String {
    resource_name
        .trim_start_matches("AWAKE_")
        .split('_')
        .map(|part| {
            let lower = part.to_ascii_lowercase();
            let mut chars = lower.chars();
            chars
                .next()
                .map(|c| c.to_ascii_uppercase().to_string() + chars.as_str())
                .unwrap_or_default()
        })
        .collect()
}

fn find_localized(dir: &Path) -> Vec<(String, PathBuf)> {
    let mut found = Vec::new();
    let Ok(entries) = fs::read_dir(dir) else {
        return found;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        let name = entry.file_name().to_string_lossy().into_owned();
        if path.is_dir() && looks_like_language(&name) {
            let candidate = path.join("Resources.resx");
            if candidate.is_file() {
                found.push((name, candidate));
            }
        } else if let Some(language) = name
            .strip_prefix("Resources.")
            .and_then(|n| n.strip_suffix(".resx"))
            && looks_like_language(language)
        {
            found.push((language.to_owned(), path));
        }
    }
    found.sort();
    found
}

fn looks_like_language(name: &str) -> bool {
    let mut parts = name.split('-');
    let primary = parts.next().unwrap_or_default();
    (2..=3).contains(&primary.len())
        && primary.chars().all(|c| c.is_ascii_alphabetic())
        && parts.all(|p| !p.is_empty() && p.len() <= 8 && p.chars().all(|c| c.is_ascii_alphanumeric()))
}

/// Minimal resx reader: `<data name="X"> ... <value>text</value> ... </data>`.
fn parse_resx(path: &Path) -> Vec<(String, String)> {
    let text = fs::read_to_string(path).unwrap_or_else(|e| panic!("cannot read {}: {e}", path.display()));
    let mut entries = Vec::new();
    let mut rest = text.as_str();
    while let Some(start) = rest.find("<data ") {
        rest = &rest[start..];
        let end = rest
            .find("</data>")
            .unwrap_or_else(|| panic!("unterminated <data> in {}", path.display()));
        let block = &rest[..end];
        rest = &rest[end + "</data>".len()..];

        let name =
            attribute(block, "name").unwrap_or_else(|| panic!("<data> without name in {}", path.display()));
        let value = block
            .find("<value>")
            .and_then(|v| block[v + 7..].find("</value>").map(|e| &block[v + 7..v + 7 + e]))
            .unwrap_or("");
        entries.push((name, unescape(value)));
    }
    entries
}

fn attribute(block: &str, attr: &str) -> Option<String> {
    let needle = format!("{attr}=\"");
    let start = block.find(&needle)? + needle.len();
    let len = block[start..].find('"')?;
    Some(unescape(&block[start..start + len]))
}

fn unescape(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    let mut rest = text;
    while let Some(amp) = rest.find('&') {
        out.push_str(&rest[..amp]);
        rest = &rest[amp..];
        let Some(semi) = rest.find(';') else { break };
        let entity = &rest[1..semi];
        let decoded = match entity {
            "lt" => Some('<'),
            "gt" => Some('>'),
            "amp" => Some('&'),
            "quot" => Some('"'),
            "apos" => Some('\''),
            _ if entity.starts_with("#x") => u32::from_str_radix(&entity[2..], 16)
                .ok()
                .and_then(char::from_u32),
            _ if entity.starts_with('#') => entity[1..].parse().ok().and_then(char::from_u32),
            _ => None,
        };
        match decoded {
            Some(c) => {
                out.push(c);
                rest = &rest[semi + 1..];
            }
            None => {
                out.push('&');
                rest = &rest[1..];
            }
        }
    }
    out.push_str(rest);
    out.replace("\r\n", "\n")
}
