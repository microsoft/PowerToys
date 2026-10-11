//! Turns the menu model from `awake_core::tray` into a Win32 popup menu and shows it.
//!
//! The menu is built fresh each time it opens and destroyed when it closes, so it can never be
//! out of date or touched by another thread.

use std::{mem, ptr};

use awake_core::tray::{Check, MenuItem};
use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CreatePopupMenu, DestroyMenu, GetSystemMetrics, HMENU, InsertMenuItemW, MENUINFO, MENUITEMINFOW,
    MFS_CHECKED, MFS_DISABLED, MFT_RADIOCHECK, MFT_RIGHTJUSTIFY, MFT_RIGHTORDER, MFT_SEPARATOR, MFT_STRING,
    MIIM_FTYPE, MIIM_ID, MIIM_STATE, MIIM_STRING, MIIM_SUBMENU, MIM_STYLE, MNS_AUTODISMISS, PostMessageW,
    SM_MENUDROPALIGNMENT, SetForegroundWindow, SetMenuInfo, TPM_BOTTOMALIGN, TPM_LAYOUTRTL, TPM_LEFTALIGN,
    TPM_NONOTIFY, TPM_RETURNCMD, TPM_RIGHTALIGN, TPM_RIGHTBUTTON, TrackPopupMenuEx, WM_NULL,
};

use super::wide::wide;

#[derive(Debug)]
pub struct PopupMenu(HMENU);

impl Drop for PopupMenu {
    fn drop(&mut self) {
        // SAFETY: destroying our own menu also destroys its submenus.
        unsafe { DestroyMenu(self.0) };
    }
}

pub fn build(items: &[MenuItem], right_to_left: bool) -> Option<PopupMenu> {
    let menu = PopupMenu(create()?);
    append(menu.0, items, right_to_left)?;
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
    Some(menu)
}

/// Shows the menu at a screen point and returns the chosen command id, or 0.
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

fn create() -> Option<HMENU> {
    // SAFETY: plain call.
    let menu = unsafe { CreatePopupMenu() };
    (!menu.is_null()).then_some(menu)
}

fn append(menu: HMENU, items: &[MenuItem], right_to_left: bool) -> Option<()> {
    let direction = if right_to_left {
        MFT_RIGHTORDER | MFT_RIGHTJUSTIFY
    } else {
        0
    };
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
                info.fType = MFT_STRING | direction | radio(*check);
                info.fState = state(*check, *enabled);
                info.wID = u32::from(*id);
                info.dwTypeData = text.as_mut_ptr();
            }
            MenuItem::Submenu { label, check, items } => {
                let submenu = create()?;
                if append(submenu, items, right_to_left).is_none() {
                    // SAFETY: not yet attached, so we still own it.
                    unsafe { DestroyMenu(submenu) };
                    return None;
                }
                text = wide(label);
                info.fMask = MIIM_FTYPE | MIIM_STRING | MIIM_STATE | MIIM_SUBMENU;
                info.fType = MFT_STRING | direction | radio(*check);
                info.fState = state(*check, true);
                info.hSubMenu = submenu;
                info.dwTypeData = text.as_mut_ptr();
            }
        }
        // SAFETY: `info` and the text it points to are valid for the call; the menu copies them.
        if unsafe { InsertMenuItemW(menu, position as u32, 1, &info) } == 0 {
            if !info.hSubMenu.is_null() {
                // SAFETY: insertion failed, so the submenu is still ours.
                unsafe { DestroyMenu(info.hSubMenu) };
            }
            return None;
        }
    }
    Some(())
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
