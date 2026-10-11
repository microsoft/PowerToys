//! Who may share the named kernel objects Awake uses across processes.
//!
//! The instance mutex and the exit event have fixed names in the session's `Local\` namespace,
//! so any process in the session can create them first, including one at low integrity such as
//! a sandboxed browser tab. Opening an object made that way would let its creator hold the
//! instance lock for good or signal Awake to exit. So Awake creates its objects for the current
//! user and SYSTEM only, with a medium integrity label, and before using an object that already
//! existed it checks who made it.

use std::ffi::c_void;
use std::io;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::ptr;

use windows_sys::Win32::Foundation::{ERROR_ACCESS_DENIED, LocalFree};
use windows_sys::Win32::Security::Authorization::{
    ConvertSidToStringSidW, ConvertStringSecurityDescriptorToSecurityDescriptorW, GetSecurityInfo,
    SDDL_REVISION_1, SE_KERNEL_OBJECT,
};
use windows_sys::Win32::Security::{
    ACE_HEADER, ACL, EqualSid, GetAce, GetSidSubAuthority, GetSidSubAuthorityCount, GetTokenInformation,
    IsWellKnownSid, LABEL_SECURITY_INFORMATION, OWNER_SECURITY_INFORMATION, PSID, SECURITY_ATTRIBUTES,
    SYSTEM_MANDATORY_LABEL_ACE, TOKEN_QUERY, TOKEN_USER, TokenUser, WinBuiltinAdministratorsSid,
    WinLocalSystemSid,
};
use windows_sys::Win32::System::Threading::{GetCurrentProcess, OpenProcessToken};

use super::last_error;
use super::wide::{from_nul_terminated, wide};

// windows-sys keeps these two in `Win32_System_SystemServices`, which Awake needs for nothing else.
const SYSTEM_MANDATORY_LABEL_ACE_TYPE: u8 = 0x11;
const SECURITY_MANDATORY_MEDIUM_RID: u32 = 0x2000;

/// A block `LocalAlloc` returned to us, such as a security descriptor; freed when dropped.
struct LocalBox(*mut c_void);

impl Drop for LocalBox {
    fn drop(&mut self) {
        if !self.0.is_null() {
            // SAFETY: the pointer came from an API documented to return LocalAlloc memory, and
            // this is its only owner.
            unsafe { LocalFree(self.0) };
        }
    }
}

/// The security descriptor for the named objects Awake creates.
pub struct Descriptor(LocalBox);

impl Descriptor {
    /// The user owns the object and gets full access, so the regular and the elevated Awake of
    /// one user still find each other; SYSTEM gets full access too. The medium label with
    /// no-write-up keeps lower-integrity processes from signaling or taking the object.
    pub fn for_current_user() -> io::Result<Self> {
        let user = UserSid::current()?.to_string_sid()?;
        Self::from_sddl(&format!("O:{user}D:(A;;GA;;;{user})(A;;GA;;;SY)S:(ML;;NW;;;ME)"))
    }

    fn from_sddl(sddl: &str) -> io::Result<Self> {
        let sddl = wide(sddl);
        let mut raw = ptr::null_mut();
        // SAFETY: `sddl` is NUL-terminated and outlives the call; `raw` receives a LocalAlloc
        // block that `LocalBox` frees.
        let converted = unsafe {
            ConvertStringSecurityDescriptorToSecurityDescriptorW(
                sddl.as_ptr(),
                SDDL_REVISION_1,
                &raw mut raw,
                ptr::null_mut(),
            )
        };
        if converted == 0 {
            return Err(last_error());
        }
        Ok(Descriptor(LocalBox(raw)))
    }

    /// Attributes for a `Create*W` call; they borrow the descriptor, so keep `self` alive.
    pub fn attributes(&self) -> SECURITY_ATTRIBUTES {
        SECURITY_ATTRIBUTES {
            nLength: size_of::<SECURITY_ATTRIBUTES>() as u32,
            lpSecurityDescriptor: self.0.0,
            bInheritHandle: 0,
        }
    }
}

/// The current process user's SID, kept inside the `TOKEN_USER` block it came from.
struct UserSid {
    // `u64` units keep the block aligned for `TOKEN_USER`, which starts with a pointer.
    block: Vec<u64>,
}

impl UserSid {
    fn current() -> io::Result<Self> {
        let mut token = ptr::null_mut();
        // SAFETY: the pseudo-handle needs no closing; `token` receives a handle closed below.
        if unsafe { OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw mut token) } == 0 {
            return Err(last_error());
        }
        // SAFETY: a fresh token handle we own.
        let token = unsafe { OwnedHandle::from_raw_handle(token) };
        let mut needed = 0;
        // SAFETY: a size query: no buffer, and `needed` receives the size.
        unsafe {
            GetTokenInformation(
                token.as_raw_handle(),
                TokenUser,
                ptr::null_mut(),
                0,
                &raw mut needed,
            )
        };
        let mut block = vec![0u64; (needed as usize).div_ceil(size_of::<u64>())];
        // SAFETY: `block` holds at least `needed` bytes and is aligned for `TOKEN_USER`.
        let filled = unsafe {
            GetTokenInformation(
                token.as_raw_handle(),
                TokenUser,
                block.as_mut_ptr().cast(),
                needed,
                &raw mut needed,
            )
        };
        if filled == 0 {
            return Err(last_error());
        }
        Ok(UserSid { block })
    }

    fn sid(&self) -> PSID {
        // SAFETY: `block` holds the `TOKEN_USER` filled in `current`; its SID points into the
        // same heap block, which lives as long as `self`.
        unsafe { (*self.block.as_ptr().cast::<TOKEN_USER>()).User.Sid }
    }

    fn to_string_sid(&self) -> io::Result<String> {
        sid_string(self.sid())
    }
}

/// `S-1-5-...`, for the descriptor and for log lines.
fn sid_string(sid: PSID) -> io::Result<String> {
    let mut text = ptr::null_mut();
    // SAFETY: `sid` is a valid SID; `text` receives a LocalAlloc string freed by `LocalBox`.
    if unsafe { ConvertSidToStringSidW(sid, &raw mut text) } == 0 {
        return Err(last_error());
    }
    let _free = LocalBox(text.cast());
    // SAFETY: on success `text` is a NUL-terminated string, alive until `_free` drops.
    Ok(unsafe { from_nul_terminated(text) })
}

/// Checks an object that already existed when Awake asked to create `name`. It is used only
/// when the current user, an administrator (an elevated runner) or SYSTEM made it, at medium
/// integrity or above; anything else was planted, and Awake refuses it as access denied.
pub fn verify_existing(object: &OwnedHandle, name: &str) -> io::Result<()> {
    let mut owner: PSID = ptr::null_mut();
    let mut sacl: *mut ACL = ptr::null_mut();
    let mut descriptor = ptr::null_mut();
    // SAFETY: `object` is a valid handle opened with READ_CONTROL (the `Create*W` calls ask for
    // all access); the out pointers point into `descriptor`, which `_free` releases last.
    let status = unsafe {
        GetSecurityInfo(
            object.as_raw_handle(),
            SE_KERNEL_OBJECT,
            OWNER_SECURITY_INFORMATION | LABEL_SECURITY_INFORMATION,
            &raw mut owner,
            ptr::null_mut(),
            ptr::null_mut(),
            &raw mut sacl,
            &raw mut descriptor,
        )
    };
    if status != 0 {
        return Err(io::Error::from_raw_os_error(status as i32));
    }
    let _free = LocalBox(descriptor);
    // SAFETY: `sacl` is null or the label ACL inside `descriptor`.
    let integrity = unsafe { integrity_level(sacl) };
    let user = UserSid::current()?;
    if integrity >= SECURITY_MANDATORY_MEDIUM_RID && owner_is_trusted(owner, &user) {
        return Ok(());
    }
    let owner = if owner.is_null() {
        "none".to_owned()
    } else {
        sid_string(owner).unwrap_or_else(|_| "unreadable".to_owned())
    };
    log::warn!(
        "{name} already exists but was not created by Awake for this user (owner {owner}, \
         integrity level {integrity:#x}); refusing to use it."
    );
    Err(io::Error::from_raw_os_error(ERROR_ACCESS_DENIED as i32))
}

fn owner_is_trusted(owner: PSID, user: &UserSid) -> bool {
    if owner.is_null() {
        return false;
    }
    // SAFETY: both SIDs are valid for the duration of the calls.
    unsafe {
        EqualSid(owner, user.sid()) != 0
            || IsWellKnownSid(owner, WinBuiltinAdministratorsSid) != 0
            || IsWellKnownSid(owner, WinLocalSystemSid) != 0
    }
}

/// The integrity level in an object's label ACL; an object without a label counts as medium,
/// as it does for Windows' own checks.
///
/// # Safety
///
/// `sacl` must be null or point to a valid ACL.
unsafe fn integrity_level(sacl: *const ACL) -> u32 {
    if sacl.is_null() {
        return SECURITY_MANDATORY_MEDIUM_RID;
    }
    // SAFETY: the caller guarantees a valid ACL.
    let count = unsafe { (*sacl).AceCount };
    for index in 0..count {
        let mut ace = ptr::null_mut();
        // SAFETY: `index` is below the ACL's ACE count.
        if unsafe { GetAce(sacl, u32::from(index), &raw mut ace) } == 0 {
            continue;
        }
        // SAFETY: every ACE starts with an `ACE_HEADER`.
        if unsafe { (*ace.cast::<ACE_HEADER>()).AceType } != SYSTEM_MANDATORY_LABEL_ACE_TYPE {
            continue;
        }
        // SAFETY: a label ACE's SID starts at `SidStart` and runs to the end of the ACE; the
        // integrity level is its last sub-authority.
        return unsafe {
            let sid: PSID = (&raw mut (*ace.cast::<SYSTEM_MANDATORY_LABEL_ACE>()).SidStart).cast();
            match u32::from(*GetSidSubAuthorityCount(sid)).checked_sub(1) {
                Some(last) => *GetSidSubAuthority(sid, last),
                None => 0,
            }
        };
    }
    SECURITY_MANDATORY_MEDIUM_RID
}

#[cfg(test)]
pub(crate) mod tests {
    use windows_sys::Win32::Security::{
        CreateWellKnownSid, SECURITY_MAX_SID_SIZE, WELL_KNOWN_SID_TYPE, WinWorldSid,
    };
    use windows_sys::Win32::System::Threading::CreateMutexW;

    use super::*;

    /// A descriptor like the one a low-integrity process gets by default, for planting objects.
    pub(crate) fn low_integrity() -> Descriptor {
        Descriptor::from_sddl("D:(A;;GA;;;WD)S:(ML;;NW;;;LW)").unwrap()
    }

    fn label_of(object: &OwnedHandle) -> u32 {
        let mut sacl: *mut ACL = ptr::null_mut();
        let mut descriptor = ptr::null_mut();
        // SAFETY: valid handle with READ_CONTROL; the ACL lives in `descriptor`.
        let status = unsafe {
            GetSecurityInfo(
                object.as_raw_handle(),
                SE_KERNEL_OBJECT,
                LABEL_SECURITY_INFORMATION,
                ptr::null_mut(),
                ptr::null_mut(),
                ptr::null_mut(),
                &raw mut sacl,
                &raw mut descriptor,
            )
        };
        assert_eq!(status, 0);
        let _free = LocalBox(descriptor);
        // SAFETY: as above.
        unsafe { integrity_level(sacl) }
    }

    fn mutex_with(descriptor: &Descriptor, name: &str) -> OwnedHandle {
        let attributes = descriptor.attributes();
        let name = wide(name);
        // SAFETY: the attributes and name outlive the call.
        let raw = unsafe { CreateMutexW(&raw const attributes, 0, name.as_ptr()) };
        assert!(!raw.is_null(), "{}", last_error());
        // SAFETY: fresh handle we own.
        unsafe { OwnedHandle::from_raw_handle(raw) }
    }

    fn well_known(kind: WELL_KNOWN_SID_TYPE) -> Vec<u64> {
        let mut sid = vec![0u64; SECURITY_MAX_SID_SIZE as usize / size_of::<u64>() + 1];
        let mut size = SECURITY_MAX_SID_SIZE;
        // SAFETY: `sid` holds at least `size` bytes.
        let created =
            unsafe { CreateWellKnownSid(kind, ptr::null_mut(), sid.as_mut_ptr().cast(), &raw mut size) };
        assert_ne!(created, 0, "{}", last_error());
        sid
    }

    #[test]
    fn objects_awake_creates_carry_a_medium_label_and_pass_the_check() {
        let name = format!(r"Local\AwakeSecurityTest-own-{}", std::process::id());
        let mutex = mutex_with(&Descriptor::for_current_user().unwrap(), &name);
        assert_eq!(label_of(&mutex), SECURITY_MANDATORY_MEDIUM_RID);
        verify_existing(&mutex, &name).unwrap();
    }

    #[test]
    fn a_low_integrity_object_is_refused() {
        let name = format!(r"Local\AwakeSecurityTest-low-{}", std::process::id());
        let mutex = mutex_with(&low_integrity(), &name);
        assert!(label_of(&mutex) < SECURITY_MANDATORY_MEDIUM_RID);
        let error = verify_existing(&mutex, &name).unwrap_err();
        assert_eq!(error.raw_os_error(), Some(ERROR_ACCESS_DENIED as i32));
    }

    #[test]
    fn only_the_user_administrators_and_system_are_trusted_owners() {
        let user = UserSid::current().unwrap();
        assert!(user.to_string_sid().unwrap().starts_with("S-1-5-"));
        assert!(owner_is_trusted(user.sid(), &user));
        for trusted in [WinBuiltinAdministratorsSid, WinLocalSystemSid] {
            let mut sid = well_known(trusted);
            assert!(owner_is_trusted(sid.as_mut_ptr().cast(), &user));
        }
        let mut everyone = well_known(WinWorldSid);
        assert!(!owner_is_trusted(everyone.as_mut_ptr().cast(), &user));
        assert!(!owner_is_trusted(ptr::null_mut(), &user));
    }
}
