//! Turns `Resources.resx` and its localized copies into a Rust string table.
//!
//! The resx files stay the source of truth so the PowerToys localization pipeline keeps
//! translating them. Localized files arrive either as `<lang>/Resources.resx` (Win32 layout)
//! or as `Resources.<lang>.resx`.

use std::collections::BTreeMap;
use std::fmt::Write as _;
use std::path::{Path, PathBuf};
use std::{env, fs};

#[path = "build/resx.rs"]
mod resx;

fn main() {
    let strings_dir = PathBuf::from(env::var("CARGO_MANIFEST_DIR").unwrap()).join("..");
    let neutral_path = strings_dir.join("Resources.resx");
    println!("cargo:rerun-if-changed={}", neutral_path.display());
    println!("cargo:rerun-if-changed=build/resx.rs");

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

fn parse_resx(path: &Path) -> Vec<(String, String)> {
    let text = fs::read_to_string(path).unwrap_or_else(|e| panic!("cannot read {}: {e}", path.display()));
    resx::parse(&text).unwrap_or_else(|e| panic!("{} is not a valid resx file: {e}", path.display()))
}
