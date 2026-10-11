//! The UI language: PowerToys' own choice first, then Windows'.

use awake_core::powertoys;
use windows_sys::Win32::Globalization::{GetUserPreferredUILanguages, MUI_LANGUAGE_NAME};

use super::paths::Paths;
use super::settings_store::read_bounded;
use super::wide::from_buffer;

pub fn ui_language(paths: Option<&Paths>) -> String {
    paths
        .and_then(|paths| read_bounded(&paths.language_file()).ok())
        .and_then(|bytes| powertoys::language_tag(&bytes))
        .unwrap_or_else(windows_ui_language)
}

/// Text for right-to-left languages is laid out mirrored in menus.
pub fn is_right_to_left(tag: &str) -> bool {
    let primary = tag.split('-').next().unwrap_or_default().to_ascii_lowercase();
    matches!(
        primary.as_str(),
        "ar" | "fa" | "he" | "ur" | "ps" | "ug" | "yi" | "dv" | "syr"
    )
}

fn windows_ui_language() -> String {
    let mut count = 0u32;
    let mut buffer = [0u16; 256];
    let mut len = buffer.len() as u32;
    // SAFETY: `buffer` holds `len` units; the result is a double-NUL-terminated list.
    let ok =
        unsafe { GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, &mut count, buffer.as_mut_ptr(), &mut len) }
            != 0;
    if ok { from_buffer(&buffer) } else { String::new() }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn windows_reports_a_language() {
        assert!(!windows_ui_language().is_empty());
    }

    #[test]
    fn rtl_languages() {
        assert!(is_right_to_left("he-IL"));
        assert!(is_right_to_left("ar"));
        assert!(!is_right_to_left("en-US"));
    }
}
