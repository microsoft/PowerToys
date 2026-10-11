//! Dark context menus when Windows uses dark mode.
//!
//! Win32 popup menus only follow the theme through two undocumented uxtheme exports. ZoomIt in
//! this repository uses the same ordinals; if they are missing, menus simply stay light.

use std::mem;

use windows_sys::Win32::System::LibraryLoader::{
    GetProcAddress, LOAD_LIBRARY_SEARCH_SYSTEM32, LoadLibraryExW,
};
use windows_sys::Win32::System::Registry::{HKEY_CURRENT_USER, RRF_RT_REG_DWORD, RegGetValueW};

use super::wide::wide;

const SET_PREFERRED_APP_MODE: u16 = 135;
const FLUSH_MENU_THEMES: u16 = 136;
const FORCE_DARK: i32 = 2;
const FORCE_LIGHT: i32 = 3;

type SetPreferredAppMode = unsafe extern "system" fn(i32) -> i32;
type FlushMenuThemes = unsafe extern "system" fn();

/// Matches menus to the taskbar: dark when Windows' system theme is dark.
pub fn apply_system_theme() {
    let name = wide("uxtheme.dll");
    // SAFETY: loads a system DLL from System32 only; it stays loaded for the process lifetime.
    let module = unsafe { LoadLibraryExW(name.as_ptr(), std::ptr::null_mut(), LOAD_LIBRARY_SEARCH_SYSTEM32) };
    if module.is_null() {
        return;
    }
    // SAFETY: ordinal lookups on a valid module; null results are checked.
    let (set_mode, flush) = unsafe {
        (
            GetProcAddress(module, SET_PREFERRED_APP_MODE as usize as *const u8),
            GetProcAddress(module, FLUSH_MENU_THEMES as usize as *const u8),
        )
    };
    let (Some(set_mode), Some(flush)) = (set_mode, flush) else {
        return;
    };
    // SAFETY: the ordinals have these signatures on every Windows version PowerToys supports
    // (Windows 10 2004 and later).
    unsafe {
        let set_mode: SetPreferredAppMode = mem::transmute(set_mode);
        let flush: FlushMenuThemes = mem::transmute(flush);
        set_mode(if system_uses_light_theme() {
            FORCE_LIGHT
        } else {
            FORCE_DARK
        });
        flush();
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
