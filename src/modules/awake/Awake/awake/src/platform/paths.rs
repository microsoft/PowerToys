//! Where Awake's files live, and the names of its kernel objects.

use std::path::PathBuf;
use std::{io, ptr};

use awake_core::settings::{FILE_NAME, MODULE_NAME};
use windows_sys::Win32::System::Com::CoTaskMemFree;
use windows_sys::Win32::UI::Shell::{FOLDERID_LocalAppData, KF_FLAG_DEFAULT, SHGetKnownFolderPath};

use super::hresult_error;
use super::wide::from_nul_terminated;

#[derive(Clone, Debug)]
pub struct Paths {
    /// `%LOCALAPPDATA%\Microsoft\PowerToys`.
    pub root: PathBuf,
    /// Appended to kernel object names under test; empty otherwise.
    pub object_suffix: String,
}

impl Paths {
    #[cfg(not(feature = "test-hooks"))]
    pub fn resolve() -> io::Result<Paths> {
        Ok(Paths {
            root: local_app_data()?.join("Microsoft").join("PowerToys"),
            object_suffix: String::new(),
        })
    }

    /// Under test, `hook_names::DATA_DIR` and `hook_names::OBJECT_SUFFIX` let tests run next to
    /// a real PowerToys installation without touching it.
    #[cfg(feature = "test-hooks")]
    pub fn resolve() -> io::Result<Paths> {
        let object_suffix = std::env::var(crate::hook_names::OBJECT_SUFFIX)
            .unwrap_or_default()
            .chars()
            .filter(|c| c.is_ascii_alphanumeric() || *c == '-')
            .collect();
        let root = match std::env::var_os(crate::hook_names::DATA_DIR).filter(|v| !v.is_empty()) {
            Some(dir) => PathBuf::from(dir),
            None => local_app_data()?.join("Microsoft").join("PowerToys"),
        };
        Ok(Paths { root, object_suffix })
    }

    pub fn module_dir(&self) -> PathBuf {
        self.root.join(MODULE_NAME)
    }

    pub fn settings_file(&self) -> PathBuf {
        self.module_dir().join(FILE_NAME)
    }

    pub fn language_file(&self) -> PathBuf {
        self.root.join("language.json")
    }

    pub fn log_settings_file(&self) -> PathBuf {
        self.root.join("log_settings.json")
    }

    pub fn log_dir(&self) -> PathBuf {
        self.module_dir().join("Logs")
    }

    /// Single-instance mutex.
    pub fn instance_mutex_name(&self) -> String {
        format!(
            "Local\\PowerToysAwakeInstance-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe{}",
            self.object_suffix
        )
    }

    pub fn exit_event_name(&self, shared_name: &str) -> String {
        format!("{shared_name}{}", self.object_suffix)
    }
}

/// `%LOCALAPPDATA%`, from the shell rather than the environment.
pub fn local_app_data() -> io::Result<PathBuf> {
    let mut path = ptr::null_mut();
    // SAFETY: the folder id is a valid static GUID and `path` receives a CoTaskMem string that
    // is freed below on every path.
    let hr = unsafe {
        SHGetKnownFolderPath(
            &FOLDERID_LocalAppData,
            KF_FLAG_DEFAULT as _,
            ptr::null_mut(),
            &mut path,
        )
    };
    if hr < 0 {
        // SAFETY: freeing a null or valid CoTaskMem pointer is allowed.
        unsafe { CoTaskMemFree(path as _) };
        return Err(hresult_error(hr, "SHGetKnownFolderPath"));
    }
    // SAFETY: on success `path` is a non-null, NUL-terminated string owned by us; it is read
    // before being freed and not used afterwards.
    let text = unsafe {
        let text = from_nul_terminated(path);
        CoTaskMemFree(path as _);
        text
    };
    Ok(PathBuf::from(text))
}
