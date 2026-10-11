//! Native pieces of the Awake executable:
//! - the C++ bridge over shared PowerToys headers (group policy, telemetry, kernel object names);
//! - Win32 resources: version info from `src/Version.props`, tray icons and the manifest.

use std::collections::BTreeSet;
use std::path::{Path, PathBuf};
use std::process::Command;
use std::{env, fs};

const PRODUCT_FILE_NAME: &str = "PowerToys.Awake";

fn main() {
    check_hardening();
    let manifest_dir = PathBuf::from(env::var("CARGO_MANIFEST_DIR").unwrap());
    let repo_src = plain_path(
        manifest_dir
            .join("../../../..")
            .canonicalize()
            .expect("cannot locate the repository's src folder"),
    );
    let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap());

    let version = ProductVersion::resolve(&repo_src);
    println!("cargo:rustc-env=AWAKE_PRODUCT_VERSION={}", version.display());
    println!("cargo:rustc-env=AWAKE_TELEMETRY_VERSION={}", version.telemetry());
    println!("cargo:rerun-if-env-changed=POWERTOYS_VERSION");

    compile_bridge(&manifest_dir, &repo_src);
    let resources = compile_resources(&manifest_dir, &out_dir, &version);
    println!("cargo:rustc-link-arg-bins={}", resources.display());
    // Symbols are published under the shipped file name, not Cargo's.
    println!("cargo:rustc-link-arg-bins=/PDBALTPATH:{PRODUCT_FILE_NAME}.pdb");
    // The manifest is embedded as a resource above.
    println!("cargo:rustc-link-arg-bins=/MANIFEST:NO");
    // oleaut32 only parses a legacy, non-ISO `--expire-at`; load it on that first call instead of
    // at every start. It is a KnownDLL, so the loader always takes it from System32.
    println!("cargo:rustc-link-arg-bins=/DELAYLOAD:oleaut32.dll");
    println!("cargo:rustc-link-arg-bins=delayimp.lib");
    // LOAD_LIBRARY_SEARCH_SYSTEM32: imports that are not KnownDLLs, such as wtsapi32.dll, come
    // from System32 only, never from the folder holding a copied, standalone exe.
    println!("cargo:rustc-link-arg-bins=/DEPENDENTLOADFLAG:0x800");
}

/// The hardening flags live in .cargo/config.toml, which Cargo reads only when it runs from this
/// folder and ignores when RUSTFLAGS is set. Without them the binary would still build, but
/// without Control Flow Guard or CET, tied to the VC++ redistributable or carrying its own UCRT,
/// so stop instead.
fn check_hardening() {
    println!("cargo:rerun-if-env-changed=RUSTFLAGS");
    println!("cargo:rerun-if-env-changed=CARGO_ENCODED_RUSTFLAGS");
    if env::var("CARGO_CFG_TARGET_ENV").as_deref() != Ok("msvc") {
        return;
    }
    let features = env::var("CARGO_CFG_TARGET_FEATURE").unwrap_or_default();
    let flags = env::var("CARGO_ENCODED_RUSTFLAGS").unwrap_or_default();
    let flags: Vec<&str> = flags.split('\x1f').collect();
    let mut missing = Vec::new();
    if !features.split(',').any(|f| f == "crt-static") {
        missing.push("+crt-static");
    }
    if !flags.contains(&"control-flow-guard=yes") {
        missing.push("control-flow-guard=yes");
    }
    if !flags.contains(&"link-arg=/NODEFAULTLIB:libucrt.lib")
        || !flags.contains(&"link-arg=/DEFAULTLIB:ucrt.lib")
    {
        missing.push("hybrid CRT (/NODEFAULTLIB:libucrt.lib /DEFAULTLIB:ucrt.lib)");
    }
    if env::var("CARGO_CFG_TARGET_ARCH").as_deref() == Ok("x86_64") && !flags.contains(&"link-arg=/CETCOMPAT")
    {
        missing.push("/CETCOMPAT");
    }
    assert!(
        missing.is_empty(),
        "the hardening flags from .cargo/config.toml are missing ({}): run Cargo from \
         src/modules/awake/Awake (or build Awake.vcxproj) and leave RUSTFLAGS unset",
        missing.join(", ")
    );
}

fn compile_bridge(manifest_dir: &Path, repo_src: &Path) {
    let bridge = manifest_dir.join("native/bridge.cpp");
    // cc does not report header dependencies, so follow the repository headers the bridge
    // includes, directly or not; SDK and standard headers are not in the repository and are skipped.
    let mut tracked = BTreeSet::new();
    track_includes(&bridge, repo_src, &mut tracked);
    for path in &tracked {
        println!("cargo:rerun-if-changed={}", path.display());
    }

    cc::Build::new()
        .cpp(true)
        .file(&bridge)
        .include(repo_src)
        .define("UNICODE", None)
        .define("_UNICODE", None)
        .define("NOMINMAX", None)
        .flag("/std:c++20")
        .flag("/EHsc")
        .flag("/WX")
        .flag("/sdl")
        // Match the Control Flow Guard the Rust code and Cpp.Build.props use.
        .flag("/guard:cf")
        .flag("/utf-8")
        .compile("awake_bridge");
    println!("cargo:rustc-link-lib=advapi32");
}

/// Adds `file` and every repository header it includes, recursively, to `seen`.
fn track_includes(file: &Path, repo_src: &Path, seen: &mut BTreeSet<PathBuf>) {
    if !seen.insert(file.to_path_buf()) {
        return;
    }
    let Ok(text) = fs::read_to_string(file) else {
        return;
    };
    for line in text.lines() {
        let Some(rest) = line.trim_start().strip_prefix('#') else {
            continue;
        };
        let Some(rest) = rest.trim_start().strip_prefix("include") else {
            continue;
        };
        let rest = rest.trim_start();
        // "x" looks next to the including file first, like cl.exe; <x> only on the include path.
        let (name, quoted) = if let Some(quoted) = rest.strip_prefix('"') {
            (quoted.split('"').next(), true)
        } else if let Some(angled) = rest.strip_prefix('<') {
            (angled.split('>').next(), false)
        } else {
            (None, false)
        };
        let Some(name) = name else {
            continue;
        };
        let beside = file.parent().filter(|_| quoted).map(|dir| dir.join(name));
        let found = beside
            .into_iter()
            .chain([repo_src.join(name)])
            .find(|path| path.is_file());
        if let Some(header) = found {
            track_includes(&header, repo_src, seen);
        }
    }
}

fn compile_resources(manifest_dir: &Path, out_dir: &Path, version: &ProductVersion) -> PathBuf {
    let assets = manifest_dir.join("assets");
    let icon = |name: &str| {
        let path = assets.join(name);
        println!("cargo:rerun-if-changed={}", path.display());
        rc_string(&path.display().to_string())
    };
    let manifest = manifest_dir.join("native/awake.manifest");
    println!("cargo:rerun-if-changed={}", manifest.display());

    let numbers = version.numbers();
    let dotted = format!("{}.{}.{}.{}", numbers[0], numbers[1], numbers[2], numbers[3]);
    let commas = format!("{},{},{},{}", numbers[0], numbers[1], numbers[2], numbers[3]);
    let debug_flag = if env::var("PROFILE").as_deref() == Ok("debug") {
        "0x1L"
    } else {
        "0x0L"
    };

    // Resource ids must match src/platform/tray.rs. The application icon has the lowest id.
    let script = format!(
        r#"#pragma code_page(65001)
1 ICON {awake}
101 ICON {indefinite}
102 ICON {timed}
103 ICON {expirable}
104 ICON {disabled}
1 24 {manifest}

1 VERSIONINFO
FILEVERSION {commas}
PRODUCTVERSION {commas}
FILEFLAGSMASK 0x3fL
FILEFLAGS {debug_flag}
FILEOS 0x40004L
FILETYPE 0x1L
FILESUBTYPE 0x0L
BEGIN
    BLOCK "StringFileInfo"
    BEGIN
        BLOCK "040904b0"
        BEGIN
            VALUE "CompanyName", "Microsoft Corporation"
            VALUE "FileDescription", "PowerToys Awake"
            VALUE "FileVersion", "{dotted}"
            VALUE "InternalName", "{PRODUCT_FILE_NAME}"
            VALUE "LegalCopyright", "Copyright (C) Microsoft Corporation. All rights reserved."
            VALUE "OriginalFilename", "{PRODUCT_FILE_NAME}.exe"
            VALUE "ProductName", "PowerToys"
            VALUE "ProductVersion", "{dotted}"
        END
    END
    BLOCK "VarFileInfo"
    BEGIN
        VALUE "Translation", 0x409, 1200
    END
END
"#,
        awake = icon("awake.ico"),
        indefinite = icon("indefinite.ico"),
        timed = icon("timed.ico"),
        expirable = icon("expirable.ico"),
        disabled = icon("disabled.ico"),
        manifest = rc_string(&manifest.display().to_string()),
    );

    let rc_path = out_dir.join("awake.rc");
    let res_path = out_dir.join("awake.res");
    fs::write(&rc_path, script).expect("cannot write awake.rc");
    let mut rc = find_rc();
    let status = rc
        .args(["/nologo", "/c65001", "/fo"])
        .arg(&res_path)
        .arg(&rc_path)
        .status()
        .unwrap_or_else(|e| panic!("cannot run {}: {e}", rc.get_program().display()));
    assert!(status.success(), "rc.exe failed on {}", rc_path.display());
    res_path
}

/// `canonicalize` returns `\?\` paths, which cl.exe and rc.exe do not accept.
fn plain_path(path: PathBuf) -> PathBuf {
    let text = path.to_string_lossy();
    match text.strip_prefix(r"\\?\") {
        Some(rest) if !rest.starts_with(r"UNC\") => PathBuf::from(rest),
        _ => path,
    }
}

fn rc_string(text: &str) -> String {
    format!("\"{}\"", text.replace('\\', "\\\\"))
}

/// `rc.exe` from `RC`, the SDK MSBuild selected (`WindowsSdkVerBinPath`), or the newest installed
/// Windows SDK as found by cc.
fn find_rc() -> Command {
    println!("cargo:rerun-if-env-changed=RC");
    println!("cargo:rerun-if-env-changed=WindowsSdkVerBinPath");
    if let Some(rc) = env::var_os("RC").map(PathBuf::from).filter(|p| p.is_file()) {
        return Command::new(rc);
    }
    // The build script runs on the host, so its own architecture picks the host tools.
    let host = if cfg!(target_arch = "aarch64") {
        "arm64"
    } else if cfg!(target_arch = "x86") {
        "x86"
    } else {
        "x64"
    };
    if let Some(bin) = env::var_os("WindowsSdkVerBinPath") {
        let rc = PathBuf::from(bin).join(host).join("rc.exe");
        if rc.is_file() {
            return Command::new(rc);
        }
    }
    let target = env::var("TARGET").unwrap();
    cc::windows_registry::find_tool(&target, "rc.exe")
        .expect("rc.exe not found; install the Windows SDK or set RC")
        .to_command()
}

/// The PowerToys version, resolved like src/common/version/version.vcxproj does.
struct ProductVersion {
    text: String,
}

impl ProductVersion {
    fn resolve(repo_src: &Path) -> Self {
        if let Ok(text) = env::var("POWERTOYS_VERSION")
            && !text.trim().is_empty()
        {
            return ProductVersion {
                text: text.trim().to_owned(),
            };
        }
        let props = repo_src.join("Version.props");
        println!("cargo:rerun-if-changed={}", props.display());
        let content = fs::read_to_string(&props).unwrap_or_default();
        let text = content
            .split_once("<Version>")
            .and_then(|(_, rest)| rest.split_once("</Version>"))
            .map_or_else(|| "0.0.1".to_owned(), |(version, _)| version.trim().to_owned());
        ProductVersion { text }
    }

    fn numbers(&self) -> [u32; 4] {
        let mut numbers = [0; 4];
        for (slot, part) in numbers.iter_mut().zip(self.text.split('.')) {
            *slot = part.parse().unwrap_or(0);
        }
        numbers
    }

    /// `v0.97.1`, plus the build number when there is one, like `get_product_version()`.
    fn display(&self) -> String {
        let [major, minor, revision, build] = self.numbers();
        if build == 0 {
            format!("v{major}.{minor}.{revision}")
        } else {
            format!("v{major}.{minor}.{revision}.{build}")
        }
    }

    /// `v0.97.1`, the shape the managed telemetry events reported.
    fn telemetry(&self) -> String {
        let [major, minor, revision, _] = self.numbers();
        format!("v{major}.{minor}.{revision}")
    }
}
