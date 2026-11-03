//! Buldog Boost — безпечні системні модулі.
//!
//! Експортує C-інтерфейс для C# (P/Invoke):
//!   * `bb_safe_is_protected(name)` — чи захищений процес від закриття;
//!   * `bb_safe_terminate(pid)`     — закрити процес, відмовляючи для системних;
//!   * `bb_safe_is_admin()`         — чи запущено з правами адміністратора.

use std::ffi::CStr;
use std::os::raw::c_char;

/// Коди результату `bb_safe_terminate`.
pub const OK: i32 = 0;
pub const ERR_INVALID_PID: i32 = -1;
pub const ERR_OPEN_FAILED: i32 = -2;
pub const ERR_PROTECTED: i32 = -3;
pub const ERR_TERMINATE_FAILED: i32 = -4;
pub const ERR_UNSUPPORTED: i32 = -5;

/// Процеси, які не можна закривати ніколи.
const PROTECTED: &[&str] = &[
    "system", "system idle process", "registry", "memory compression",
    "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "services.exe",
    "lsass.exe", "svchost.exe", "explorer.exe", "dwm.exe", "fontdrvhost.exe",
    "taskhostw.exe", "sihost.exe", "ctfmon.exe", "audiodg.exe", "msmpeng.exe",
    "securityhealthservice.exe", "buldogboost.exe",
];

/// Перевіряє ім'я або повний шлях процесу.
pub fn is_protected_name(name_or_path: &str) -> bool {
    let lower = name_or_path.to_ascii_lowercase();
    let base = lower.rsplit(|c| c == '\\' || c == '/').next().unwrap_or(&lower);
    if PROTECTED.contains(&base) {
        return true;
    }
    // Усе з системних папок Windows теж захищене.
    lower.contains("\\windows\\system32\\") || lower.contains("\\windows\\syswow64\\")
}

/// 1 — захищений (також для NULL), 0 — можна закривати.
#[no_mangle]
pub extern "C" fn bb_safe_is_protected(name: *const c_char) -> i32 {
    if name.is_null() {
        return 1;
    }
    // SAFETY: покажчик перевірено на NULL; викликач гарантує NUL-термінований рядок.
    let s = unsafe { CStr::from_ptr(name) }.to_string_lossy();
    is_protected_name(&s) as i32
}

#[cfg(windows)]
mod win {
    use std::ffi::c_void;
    pub type Handle = *mut c_void;
    pub const PROCESS_TERMINATE: u32 = 0x0001;
    pub const PROCESS_QUERY_LIMITED_INFORMATION: u32 = 0x1000;

    #[link(name = "kernel32")]
    extern "system" {
        pub fn OpenProcess(access: u32, inherit: i32, pid: u32) -> Handle;
        pub fn CloseHandle(h: Handle) -> i32;
        pub fn TerminateProcess(h: Handle, exit_code: u32) -> i32;
        pub fn QueryFullProcessImageNameW(h: Handle, flags: u32, buf: *mut u16, size: *mut u32) -> i32;
        pub fn GetCurrentProcessId() -> u32;
    }

    #[link(name = "shell32")]
    extern "system" {
        pub fn IsUserAnAdmin() -> i32;
    }
}

/// 0 — успіх; від'ємні значення — див. константи `ERR_*`.
#[cfg(windows)]
#[no_mangle]
pub extern "C" fn bb_safe_terminate(pid: u32) -> i32 {
    use win::*;
    // SAFETY: прості виклики WinAPI з коректними аргументами; дескриптор завжди закривається.
    unsafe {
        if pid <= 4 || pid == GetCurrentProcessId() {
            return ERR_INVALID_PID;
        }
        let h = OpenProcess(PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION, 0, pid);
        if h.is_null() {
            return ERR_OPEN_FAILED;
        }
        let mut buf = [0u16; 1024];
        let mut len = buf.len() as u32;
        // Не вдалося дізнатись, що це за процес — відмовляємо (безпека понад усе).
        if QueryFullProcessImageNameW(h, 0, buf.as_mut_ptr(), &mut len) == 0 {
            CloseHandle(h);
            return ERR_PROTECTED;
        }
        let path = String::from_utf16_lossy(&buf[..len as usize]);
        if is_protected_name(&path) {
            CloseHandle(h);
            return ERR_PROTECTED;
        }
        let ok = TerminateProcess(h, 1);
        CloseHandle(h);
        if ok == 0 { ERR_TERMINATE_FAILED } else { OK }
    }
}

#[cfg(not(windows))]
#[no_mangle]
pub extern "C" fn bb_safe_terminate(_pid: u32) -> i32 {
    ERR_UNSUPPORTED
}

/// 1 — адміністратор, 0 — ні.
#[cfg(windows)]
#[no_mangle]
pub extern "C" fn bb_safe_is_admin() -> i32 {
    // SAFETY: функція без аргументів.
    unsafe { (win::IsUserAnAdmin() != 0) as i32 }
}

#[cfg(not(windows))]
#[no_mangle]
pub extern "C" fn bb_safe_is_admin() -> i32 {
    0
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn protects_system_names_case_insensitive() {
        assert!(is_protected_name("lsass.exe"));
        assert!(is_protected_name("LSASS.EXE"));
        assert!(is_protected_name("Explorer.exe"));
    }

    #[test]
    fn protects_by_full_path() {
        assert!(is_protected_name(r"C:\Windows\System32\notepad.exe"));
        assert!(is_protected_name(r"C:\WINDOWS\SysWOW64\foo.exe"));
        assert!(is_protected_name(r"D:\apps\BuldogBoost.exe"));
    }

    #[test]
    fn allows_regular_apps() {
        assert!(!is_protected_name("chrome.exe"));
        assert!(!is_protected_name(r"C:\Program Files\Game\game.exe"));
    }

    #[test]
    fn ffi_null_is_protected() {
        assert_eq!(bb_safe_is_protected(std::ptr::null()), 1);
    }

    #[test]
    fn ffi_roundtrip() {
        let a = CString::new("svchost.exe").unwrap();
        let b = CString::new("discord.exe").unwrap();
        assert_eq!(bb_safe_is_protected(a.as_ptr()), 1);
        assert_eq!(bb_safe_is_protected(b.as_ptr()), 0);
    }
}
