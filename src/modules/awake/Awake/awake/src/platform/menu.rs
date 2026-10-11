//! Turns the menu model from `awake_core::tray_model` into a Win32 popup menu and shows it.
//!
//! The menu is built fresh each time it opens and destroyed when it closes, so it can never be
//! out of date or touched by another thread.

use std::{io, mem, ptr};

use awake_core::tray_model::{Check, MenuItem};
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CreatePopupMenu, DestroyMenu, GetSystemMetrics, HMENU, InsertMenuItemW, MENUINFO, MENUITEMINFOW,
    MFS_CHECKED, MFS_DISABLED, MFT_RADIOCHECK, MFT_SEPARATOR, MFT_STRING, MIIM_FTYPE, MIIM_ID, MIIM_STATE,
    MIIM_STRING, MIIM_SUBMENU, MIM_STYLE, MNS_AUTODISMISS, PostMessageW, SM_MENUDROPALIGNMENT,
    SetForegroundWindow, SetMenuInfo, TPM_BOTTOMALIGN, TPM_LAYOUTRTL, TPM_LEFTALIGN, TPM_NONOTIFY,
    TPM_RETURNCMD, TPM_RIGHTALIGN, TPM_RIGHTBUTTON, TrackPopupMenuEx, WM_NULL,
};

use super::last_error;
use super::wide::wide;

#[derive(Debug)]
pub struct PopupMenu(HMENU);

impl Drop for PopupMenu {
    fn drop(&mut self) {
        // SAFETY: destroying our own menu also destroys its submenus.
        unsafe { DestroyMenu(self.0) };
    }
}

/// Builds the popup. The error is the one from the call that failed, taken before any cleanup.
pub fn build(items: &[MenuItem]) -> io::Result<PopupMenu> {
    let menu = PopupMenu(create()?);
    append(menu.0, items)?;
    let info = MENUINFO {
        cbSize: mem::size_of::<MENUINFO>() as u32,
        fMask: MIM_STYLE,
        // Closes the menu when the pointer has been away from it for a while, as before.
        dwStyle: MNS_AUTODISMISS,
        // SAFETY: the remaining fields are unused for MIM_STYLE.
        ..unsafe { mem::zeroed() }
    };
    // SAFETY: valid menu and struct.
    unsafe { SetMenuInfo(menu.0, &info) };
    Ok(menu)
}

/// Shows the menu at a screen point and returns the chosen command id, or 0.
///
/// Right-to-left languages need nothing per item: `TPM_LAYOUTRTL` mirrors the whole menu,
/// submenus included, as Explorer does. The per-item `MFT_RIGHTJUSTIFY` only applies to menu
/// bars, and `MFT_RIGHTORDER` on top of a mirrored menu would flip submenus a second time.
pub fn track(menu: &PopupMenu, window: HWND, x: i32, y: i32, right_to_left: bool) -> u16 {
    // SAFETY: plain value call.
    let drop_right = unsafe { GetSystemMetrics(SM_MENUDROPALIGNMENT) } != 0;
    let mut flags = TPM_RETURNCMD | TPM_NONOTIFY | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN;
    flags |= if drop_right { TPM_RIGHTALIGN } else { TPM_LEFTALIGN };
    if right_to_left {
        flags |= TPM_LAYOUTRTL;
    }
    // SAFETY: the window belongs to this thread; foreground is required for the menu to close
    // when the user clicks elsewhere, and the WM_NULL afterwards is the documented companion.
    unsafe {
        SetForegroundWindow(window);
        let command = TrackPopupMenuEx(menu.0, flags, x, y, window, ptr::null());
        PostMessageW(window, WM_NULL, 0, 0);
        command as u16
    }
}

fn create() -> io::Result<HMENU> {
    // SAFETY: plain call.
    let menu = unsafe { CreatePopupMenu() };
    if menu.is_null() {
        Err(last_error())
    } else {
        Ok(menu)
    }
}

fn append(menu: HMENU, items: &[MenuItem]) -> io::Result<()> {
    for (position, item) in items.iter().enumerate() {
        // SAFETY: MENUITEMINFOW is plain data.
        let mut info: MENUITEMINFOW = unsafe { mem::zeroed() };
        info.cbSize = mem::size_of::<MENUITEMINFOW>() as u32;
        let mut text;
        match item {
            MenuItem::Separator => {
                info.fMask = MIIM_FTYPE;
                info.fType = MFT_SEPARATOR;
            }
            MenuItem::Command {
                id,
                label,
                check,
                enabled,
            } => {
                text = wide(label);
                info.fMask = MIIM_FTYPE | MIIM_STRING | MIIM_STATE | MIIM_ID;
                info.fType = MFT_STRING | radio(*check);
                info.fState = state(*check, *enabled);
                info.wID = u32::from(*id);
                info.dwTypeData = text.as_mut_ptr();
            }
            MenuItem::Submenu { label, check, items } => {
                let submenu = create()?;
                if let Err(error) = append(submenu, items) {
                    // SAFETY: not yet attached, so we still own it.
                    unsafe { DestroyMenu(submenu) };
                    return Err(error);
                }
                text = wide(label);
                info.fMask = MIIM_FTYPE | MIIM_STRING | MIIM_STATE | MIIM_SUBMENU;
                info.fType = MFT_STRING | radio(*check);
                info.fState = state(*check, true);
                info.hSubMenu = submenu;
                info.dwTypeData = text.as_mut_ptr();
            }
        }
        // SAFETY: `info` and the text it points to are valid for the call; the menu copies them.
        if unsafe { InsertMenuItemW(menu, position as u32, 1, &info) } == 0 {
            let error = last_error();
            if !info.hSubMenu.is_null() {
                // SAFETY: insertion failed, so the submenu is still ours.
                unsafe { DestroyMenu(info.hSubMenu) };
            }
            return Err(error);
        }
    }
    Ok(())
}

fn radio(check: Check) -> u32 {
    if check == Check::Selected {
        MFT_RADIOCHECK
    } else {
        0
    }
}

fn state(check: Check, enabled: bool) -> u32 {
    let mut state = 0;
    if check != Check::None {
        state |= MFS_CHECKED;
    }
    if !enabled {
        // MFS_DISABLED is the same value as MFS_GRAYED: drawn dimmed and not selectable.
        state |= MFS_DISABLED;
    }
    state
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn menus_with_submenus_build() {
        let items = [
            MenuItem::Command {
                id: 1,
                label: "Off".into(),
                check: Check::Selected,
                enabled: true,
            },
            MenuItem::Separator,
            MenuItem::Submenu {
                label: "Keep awake for".into(),
                check: Check::None,
                items: vec![MenuItem::Command {
                    id: 2,
                    label: "30 minutes".into(),
                    check: Check::None,
                    enabled: false,
                }],
            },
        ];
        let menu = build(&items).unwrap();
        assert!(!menu.0.is_null());
    }
}
