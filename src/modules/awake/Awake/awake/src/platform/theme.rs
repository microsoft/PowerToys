//! Dark context menus when Windows uses dark mode.
//!
//! Win32 popup menus only follow the theme through two undocumented uxtheme exports. ZoomIt in
//! this repository uses the same ordinals; if they are missing, menus simply stay light.

use std::sync::OnceLock;
use std::{mem, ptr};

use windows_sys::Win32::System::LibraryLoader::{
    GetProcAddress, LOAD_LIBRARY_SEARCH_SYSTEM32, LoadLibraryExW,
};
use windows_sys::Win32::System::Registry::{HKEY_CURRENT_USER, RRF_RT_REG_DWORD, RegGetValueW};
use windows_sys::Win32::UI::WindowsAndMessaging::{SPI_GETHIGHCONTRAST, SystemParametersInfoW};

use super::wide::wide;

const SET_PREFERRED_APP_MODE: u16 = 135;
const FLUSH_MENU_THEMES: u16 = 136;
/// `PreferredAppMode` values for ordinal 135.
const DEFAULT_MODE: i32 = 0;
const FORCE_DARK: i32 = 2;
const FORCE_LIGHT: i32 = 3;
/// `HIGHCONTRASTW.dwFlags` bit: a contrast theme is on.
const HCF_HIGHCONTRASTON: u32 = 1;

type SetPreferredAppMode = unsafe extern "system" fn(i32) -> i32;
type FlushMenuThemes = unsafe extern "system" fn();

/// `HIGHCONTRASTW`, declared here so the crate does not need the accessibility bindings.
#[repr(C)]
struct HighContrast {
    size: u32,
    flags: u32,
    default_scheme: *mut u16,
}

/// Matches menus to the taskbar: dark when Windows' system theme is dark.
pub fn apply_system_theme() {
    static EXPORTS: OnceLock<Option<(SetPreferredAppMode, FlushMenuThemes)>> = OnceLock::new();
    let Some((set_mode, flush)) = *EXPORTS.get_or_init(load_exports) else {
        return;
    };
    let mode = preferred_mode(high_contrast_on(), system_uses_light_theme());
    // SAFETY: the exports have these signatures, see `load_exports`.
    unsafe {
        set_mode(mode);
        flush();
    }
}

/// A contrast theme wins over light and dark: forcing either would draw menus in colors the
/// user did not choose, so the default mode lets them follow the contrast theme.
fn preferred_mode(high_contrast: bool, light: bool) -> i32 {
    match (high_contrast, light) {
        (true, _) => DEFAULT_MODE,
        (false, true) => FORCE_LIGHT,
        (false, false) => FORCE_DARK,
    }
}

fn high_contrast_on() -> bool {
    let mut info = HighContrast {
        size: mem::size_of::<HighContrast>() as u32,
        flags: 0,
        default_scheme: ptr::null_mut(),
    };
    // SAFETY: `info` is a HIGHCONTRASTW with cbSize set, as SPI_GETHIGHCONTRAST requires; uiParam
    // is its size, and nothing is written to the user profile.
    let ok = unsafe { SystemParametersInfoW(SPI_GETHIGHCONTRAST, info.size, (&raw mut info).cast(), 0) } != 0;
    ok && info.flags & HCF_HIGHCONTRASTON != 0
}

fn load_exports() -> Option<(SetPreferredAppMode, FlushMenuThemes)> {
    let name = wide("uxtheme.dll");
    // SAFETY: loads a system DLL from System32 only; it stays loaded for the process lifetime,
    // which keeps the function pointers below valid.
    let module = unsafe { LoadLibraryExW(name.as_ptr(), std::ptr::null_mut(), LOAD_LIBRARY_SEARCH_SYSTEM32) };
    if module.is_null() {
        return None;
    }
    // SAFETY: ordinal lookups on a valid module; null results are checked.
    let (set_mode, flush) = unsafe {
        (
            GetProcAddress(module, SET_PREFERRED_APP_MODE as usize as *const u8)?,
            GetProcAddress(module, FLUSH_MENU_THEMES as usize as *const u8)?,
        )
    };
    // SAFETY: the ordinals have these signatures on every Windows version PowerToys supports
    // (Windows 10 2004 and later).
    unsafe {
        let set_mode: SetPreferredAppMode = mem::transmute(set_mode);
        let flush: FlushMenuThemes = mem::transmute(flush);
        Some((set_mode, flush))
    }
}

fn system_uses_light_theme() -> bool {
    let key = wide(r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
    let value = wide("SystemUsesLightTheme");
    let mut data = 1u32;
    let mut size = mem::size_of::<u32>() as u32;
    // SAFETY: strings are NUL-terminated; `data` holds `size` bytes.
    let status = unsafe {
        RegGetValueW(
            HKEY_CURRENT_USER,
            key.as_ptr(),
            value.as_ptr(),
            RRF_RT_REG_DWORD,
            std::ptr::null_mut(),
            (&raw mut data).cast(),
            &mut size,
        )
    };
    status != 0 || data != 0
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn contrast_themes_keep_the_default_mode() {
        assert_eq!(preferred_mode(true, true), DEFAULT_MODE);
        assert_eq!(preferred_mode(true, false), DEFAULT_MODE);
        assert_eq!(preferred_mode(false, true), FORCE_LIGHT);
        assert_eq!(preferred_mode(false, false), FORCE_DARK);
    }

    #[test]
    fn high_contrast_query_matches_the_win32_layout() {
        // HIGHCONTRASTW: two 32-bit fields, then a pointer.
        assert_eq!(mem::size_of::<HighContrast>(), 8 + mem::size_of::<*mut u16>());
        // Answers on any machine, whatever its setting.
        let _ = high_contrast_on();
    }
}
