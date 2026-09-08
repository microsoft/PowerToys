# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

"""Compare maximized-window positioning flags in an isolated Chrome profile.

No existing Chrome windows/profiles are changed. No hooks or DLL injection.
Requires an installed Chrome and 64-bit Python on Windows; no build is needed.
"""

import argparse
import ctypes as c
from ctypes import wintypes as w
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import time

SCRIPT = Path(__file__).resolve()
MODULE = SCRIPT.parent.parent
spec = importlib.util.spec_from_file_location("wm_validation", SCRIPT.with_name("Validate-WindowManager.py"))
validation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validation)
probe = validation.probe
get_class = probe.api(probe.user32, "GetClassNameW", c.c_int, w.HWND, w.LPWSTR, c.c_int)
get_title = probe.api(probe.user32, "GetWindowTextW", c.c_int, w.HWND, w.LPWSTR, c.c_int)
get_client = probe.api(probe.user32, "GetClientRect", w.BOOL, w.HWND, c.POINTER(w.RECT))


def title(hwnd):
    buffer = c.create_unicode_buffer(1024)
    get_title(hwnd, buffer, len(buffer))
    return buffer.value


def sample(hwnd):
    state = probe.snapshot(hwnd)
    client = w.RECT()
    probe.checked(get_client(hwnd, c.byref(client)))
    state["client"] = probe.rect_values(client)
    state["title"] = title(hwnd)
    return state


def observe(hwnd, seconds=1.0):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        time.sleep(0.05)
    return sample(hwnd)


def set_visible(hwnd, desired, flags):
    before = probe.snapshot(hwnd)
    outer, visible = before["outer"], before["visible"]
    if visible is None:
        raise RuntimeError("Chrome did not expose DWM frame bounds.")
    adjusted = [
        desired[0] - (visible[0] - outer[0]),
        desired[1] - (visible[1] - outer[1]),
        desired[2] + (outer[2] - visible[2]),
        desired[3] + (outer[3] - visible[3]),
    ]
    probe.checked(probe.set_position(hwnd, None, adjusted[0], adjusted[1],
                                    adjusted[2] - adjusted[0], adjusted[3] - adjusted[1], flags))
    return adjusted


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--chrome", type=Path, default=Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"))
    parser.add_argument("--report", type=Path, default=MODULE / "artifacts/chrome-maximize-probe/result.json")
    parser.add_argument("--keep-open-seconds", type=int, default=0)
    args = parser.parse_args()
    probe.checked(probe.set_dpi(c.c_void_p(-4)))
    if not args.chrome.is_file():
        raise RuntimeError("Chrome executable is unavailable.")
    output = args.report.resolve().parent
    output.mkdir(parents=True, exist_ok=True)
    profile = Path(tempfile.mkdtemp(prefix="isolated-profile-", dir=output))
    page = output / "resize-test.html"
    page.write_text("""<!doctype html><meta charset="utf-8"><title>RegionMirror Chrome probe</title>
<style>html,body{margin:0;height:100%;overflow:hidden}body{display:flex;font:28px sans-serif}
section{width:50%;display:grid;place-items:center}.left{background:#f05252}.right{background:#368be8}
#size{position:fixed;top:16px;left:16px;background:white;padding:12px}</style>
<section class="left">LEFT</section><section class="right">RIGHT</section><output id="size"></output>
<script>function update(){const s=innerWidth+"x"+innerHeight;document.title="RegionMirror Chrome probe "+s;
document.getElementById("size").textContent=s}onresize=update;update()</script>""", encoding="utf-8")
    log = open(output / "chrome.log", "w", encoding="utf-8")
    process = subprocess.Popen([
        str(args.chrome), "--user-data-dir=" + str(profile), "--no-first-run",
        "--no-default-browser-check", "--disable-background-networking",
        "--new-window", page.as_uri(),
    ], stdout=log, stderr=log)
    hwnd = None
    evidence = {"profile": str(profile), "browserPid": process.pid, "cases": []}
    try:
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            windows = validation.owned_top_windows(process)
            for window in windows:
                class_name = c.create_unicode_buffer(256)
                get_class(window, class_name, len(class_name))
                if class_name.value == "Chrome_WidgetWin_1" and "RegionMirror Chrome probe" in title(window):
                    hwnd = window
                    break
            if hwnd:
                break
            time.sleep(0.1)
        if not hwnd:
            raise RuntimeError("The isolated Chrome test window did not become ready.")
        evidence["hwnd"] = hwnd
        screens = probe.monitors()
        primary = screens[0]["work"]
        initial = [primary[0] + 80, primary[1] + 80, 1000, 750]
        desired = [primary[0] + 300, primary[1] + 180, primary[0] + 1300, primary[1] + 880]
        for name, flags in [("current-flags", 0x4214), ("no-send-changing", 0x4614)]:
            probe.command(hwnd, 0xF120)
            probe.checked(probe.set_position(hwnd, None, *initial, 0x0014))
            baseline = observe(hwnd, 0.5)
            probe.command(hwnd, 0xF030)
            maximized = observe(hwnd, 0.5)
            requested_outer = set_visible(hwnd, desired, flags)
            resized = observe(hwnd, 1.0)
            probe.command(hwnd, 0xF120)
            restored = observe(hwnd, 0.5)
            checks = {
                "visibleRegionReached": resized["visible"] == desired,
                "nativeMaximizePreserved": resized["isZoomed"] and resized["styleMaximized"] and resized["showCmd"] == 3,
                "normalPlacementPreserved": resized["normalPlacement"] == baseline["normalPlacement"],
                "nativeRestoreSucceeded": not restored["isZoomed"] and restored["outer"] == baseline["outer"],
                "pageReceivedResize": resized["title"] != maximized["title"] and "RegionMirror Chrome probe" in resized["title"],
            }
            evidence["cases"].append({"name": name, "flags": flags, "desiredVisible": desired,
                                      "requestedOuter": requested_outer, "baseline": baseline,
                                      "maximized": maximized, "resized": resized, "restored": restored,
                                      "checks": checks, "passed": all(checks.values())})
            print(name + ": " + json.dumps(checks), flush=True)
        evidence["candidatePassed"] = evidence["cases"][-1]["passed"]
        if args.keep_open_seconds:
            probe.command(hwnd, 0xF030)
            observe(hwnd, 0.5)
            set_visible(hwnd, desired, 0x4614)
            args.report.write_text(json.dumps(evidence, indent=2), encoding="utf-8")
            print("Chrome preview ready", flush=True)
            time.sleep(min(120, max(0, args.keep_open_seconds)))
    finally:
        if hwnd and process.poll() is None:
            result = c.c_size_t()
            probe.send_timeout(hwnd, 0x0010, 0, 0, 0x0002, 2000, c.byref(result))
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.terminate()  # Only the browser launched with this fresh isolated profile.
            process.wait(timeout=5)
        evidence["browserExited"] = process.poll() is not None
        args.report.write_text(json.dumps(evidence, indent=2), encoding="utf-8")
        log.close()
    return 0 if evidence.get("candidatePassed") else 1


if __name__ == "__main__":
    raise SystemExit(main())
