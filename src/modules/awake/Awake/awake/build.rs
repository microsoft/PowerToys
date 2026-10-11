//! Native pieces of the Awake executable:
//! - the C++ bridge over shared PowerToys headers (group policy, telemetry, kernel object names);
//! - Win32 resources: version info from `src/Version.props`, tray icons and the manifest.

use std::path::{Path, PathBuf};
use std::process::Command;
use std::{env, fs};

const PRODUCT_FILE_NAME: &str = "PowerToys.Awake";

fn main() {
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
}

fn compile_bridge(manifest_dir: &Path, repo_src: &Path) {
    let bridge = manifest_dir.join("native/bridge.cpp");
    for header in [
        "common/Telemetry/TraceBase.h",
        "common/Telemetry/TraceLoggingDefines.h",
        "common/utils/gpo.h",
        "common/interop/shared_constants.h",
    ] {
        println!("cargo:rerun-if-changed={}", repo_src.join(header).display());
    }
    println!("cargo:rerun-if-changed={}", bridge.display());

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
        .flag("/utf-8")
        .compile("awake_bridge");
    println!("cargo:rustc-link-lib=advapi32");
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
    let rc = find_rc();
    let status = Command::new(&rc)
        .args(["/nologo", "/c65001", "/fo"])
        .arg(&res_path)
        .arg(&rc_path)
        .status()
        .unwrap_or_else(|e| panic!("cannot run {}: {e}", rc.display()));
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

/// rc.exe from `RC`, the Visual Studio environment, or the newest installed Windows SDK.
fn find_rc() -> PathBuf {
    println!("cargo:rerun-if-env-changed=RC");
    if let Some(rc) = env::var_os("RC").map(PathBuf::from).filter(|p| p.is_file()) {
        return rc;
    }
    let host = match env::var("PROCESSOR_ARCHITECTURE")
        .unwrap_or_default()
        .to_ascii_lowercase()
        .as_str()
    {
        "arm64" => "arm64",
        "x86" => "x86",
        _ => "x64",
    };
    if let Some(bin) = env::var_os("WindowsSdkVerBinPath") {
        let rc = PathBuf::from(bin).join(host).join("rc.exe");
        if rc.is_file() {
            return rc;
        }
    }
    let kits_root = Command::new("reg")
        .args([
            "query",
            r"HKLM\SOFTWARE\Microsoft\Windows Kits\Installed Roots",
            "/v",
            "KitsRoot10",
        ])
        .output()
        .ok()
        .and_then(|out| String::from_utf8(out.stdout).ok())
        .and_then(|text| {
            text.lines()
                .find_map(|line| line.split_once("REG_SZ").map(|(_, v)| PathBuf::from(v.trim())))
        })
        .unwrap_or_else(|| PathBuf::from(r"C:\Program Files (x86)\Windows Kits\10"));
    let mut candidates: Vec<PathBuf> = fs::read_dir(kits_root.join("bin"))
        .into_iter()
        .flatten()
        .flatten()
        .map(|entry| entry.path().join(host).join("rc.exe"))
        .filter(|path| path.is_file())
        .collect();
    candidates.sort_by_key(|path| sdk_version(path));
    candidates
        .pop()
        .expect("rc.exe not found; install the Windows SDK or set RC")
}

fn sdk_version(rc: &Path) -> Vec<u32> {
    rc.ancestors()
        .nth(2)
        .and_then(|dir| dir.file_name())
        .map(|name| {
            name.to_string_lossy()
                .split('.')
                .filter_map(|p| p.parse().ok())
                .collect()
        })
        .unwrap_or_default()
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
            .map(|(version, _)| version.trim().to_owned())
            .unwrap_or_else(|| "0.0.1".to_owned());
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
