# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

"""Windows-only, no-build experiment: externally resize a standard maximized window.

The child owns a normal WS_OVERLAPPEDWINDOW and delegates sizing to DefWindowProc.
The parent changes it through public APIs; no hooks, DLL injection or user apps.
Run with a 64-bit Python interpreter and --report <output.json>.
"""

import argparse
import ctypes as c
from ctypes import wintypes as w
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading
import time


user32 = c.WinDLL("user32", use_last_error=True)
kernel32 = c.WinDLL("kernel32", use_last_error=True)
dwmapi = c.WinDLL("dwmapi")
LRESULT = c.c_ssize_t
WPARAM = c.c_size_t
LPARAM = c.c_ssize_t
WNDPROC = c.WINFUNCTYPE(LRESULT, w.HWND, w.UINT, WPARAM, LPARAM)


class WNDCLASS(c.Structure):
    _fields_ = [
        ("style", w.UINT), ("procedure", WNDPROC), ("class_extra", c.c_int),
        ("window_extra", c.c_int), ("instance", w.HINSTANCE),
        ("icon", w.HICON), ("cursor", w.HANDLE), ("background", w.HBRUSH),
        ("menu", w.LPCWSTR), ("name", w.LPCWSTR),
    ]


class Placement(c.Structure):
    _fields_ = [
        ("length", w.UINT), ("flags", w.UINT), ("show", w.UINT),
        ("minimum", w.POINT), ("maximum", w.POINT), ("normal", w.RECT),
    ]


class MonitorInfo(c.Structure):
    _fields_ = [
        ("size", w.DWORD), ("monitor", w.RECT), ("work", w.RECT),
        ("flags", w.DWORD), ("device", w.WCHAR * 32),
    ]


def api(library, name, result, *arguments):
    function = getattr(library, name)
    function.restype = result
    function.argtypes = arguments
    return function


set_dpi = api(user32, "SetProcessDpiAwarenessContext", w.BOOL, c.c_void_p)
default_proc = api(user32, "DefWindowProcW", LRESULT, w.HWND, w.UINT, WPARAM, LPARAM)
register_class = api(user32, "RegisterClassW", w.ATOM, c.POINTER(WNDCLASS))
create_window = api(
    user32, "CreateWindowExW", w.HWND, w.DWORD, w.LPCWSTR, w.LPCWSTR,
    w.DWORD, c.c_int, c.c_int, c.c_int, c.c_int,
    w.HWND, w.HMENU, w.HINSTANCE, c.c_void_p,
)
module_handle = api(kernel32, "GetModuleHandleW", w.HMODULE, w.LPCWSTR)
show_window = api(user32, "ShowWindow", w.BOOL, w.HWND, c.c_int)
post_quit = api(user32, "PostQuitMessage", None, c.c_int)
get_message = api(user32, "GetMessageW", c.c_int, c.POINTER(w.MSG), w.HWND, w.UINT, w.UINT)
translate_message = api(user32, "TranslateMessage", w.BOOL, c.POINTER(w.MSG))
dispatch_message = api(user32, "DispatchMessageW", LRESULT, c.POINTER(w.MSG))
get_rect = api(user32, "GetWindowRect", w.BOOL, w.HWND, c.POINTER(w.RECT))
get_placement = api(user32, "GetWindowPlacement", w.BOOL, w.HWND, c.POINTER(Placement))
is_zoomed = api(user32, "IsZoomed", w.BOOL, w.HWND)
get_style = api(user32, "GetWindowLongPtrW", c.c_ssize_t, w.HWND, c.c_int)
get_dpi = api(user32, "GetDpiForWindow", w.UINT, w.HWND)
get_owner = api(user32, "GetWindowThreadProcessId", w.DWORD, w.HWND, c.POINTER(w.DWORD))
send_timeout = api(
    user32, "SendMessageTimeoutW", LRESULT, w.HWND, w.UINT, WPARAM, LPARAM,
    w.UINT, w.UINT, c.POINTER(c.c_size_t),
)
set_position = api(
    user32, "SetWindowPos", w.BOOL, w.HWND, w.HWND,
    c.c_int, c.c_int, c.c_int, c.c_int, w.UINT,
)
get_frame = api(
    dwmapi, "DwmGetWindowAttribute", c.c_long,
    w.HWND, w.DWORD, c.c_void_p, w.DWORD,
)
MONITORPROC = c.WINFUNCTYPE(w.BOOL, w.HMONITOR, w.HDC, c.POINTER(w.RECT), LPARAM)
enum_monitors = api(user32, "EnumDisplayMonitors", w.BOOL, w.HDC, c.POINTER(w.RECT), MONITORPROC, LPARAM)
get_monitor = api(user32, "GetMonitorInfoW", w.BOOL, w.HMONITOR, c.POINTER(MonitorInfo))


def checked(result):
    if not result:
        raise c.WinError(c.get_last_error())
    return result


def rect_values(rect):
    return [rect.left, rect.top, rect.right, rect.bottom]


def snapshot(hwnd):
    outer = w.RECT()
    placement = Placement()
    placement.length = c.sizeof(placement)
    checked(get_rect(hwnd, c.byref(outer)))
    checked(get_placement(hwnd, c.byref(placement)))
    visible = w.RECT()
    visible_ok = get_frame(hwnd, 9, c.byref(visible), c.sizeof(visible)) == 0
    return {
        "outer": rect_values(outer),
        "visible": rect_values(visible) if visible_ok else None,
        "isZoomed": bool(is_zoomed(hwnd)),
        "styleMaximized": bool(get_style(hwnd, -16) & 0x01000000),
        "showCmd": placement.show,
        "normalPlacement": rect_values(placement.normal),
        "dpi": get_dpi(hwnd),
    }


def settled(hwnd, predicate, timeout=2.0):
    deadline = time.monotonic() + timeout
    previous = None
    unchanged_since = time.monotonic()
    while True:
        current = snapshot(hwnd)
        if current != previous:
            previous = current
            unchanged_since = time.monotonic()
        if predicate(current) and time.monotonic() - unchanged_since >= 0.25:
            return current
        if time.monotonic() >= deadline:
            return current
        time.sleep(0.02)


def command(hwnd, system_command):
    result = c.c_size_t()
    checked(send_timeout(hwnd, 0x0112, system_command, 0, 0x0002, 2000, c.byref(result)))


def child(initial):
    @WNDPROC
    def window_proc(hwnd, message, wp, lp):
        if message == 0x0002:  # WM_DESTROY
            post_quit(0)
            return 0
        return default_proc(hwnd, message, wp, lp)

    instance = module_handle(None)
    definition = WNDCLASS(
        0, window_proc, 0, 0, instance, None, None, 6, None,
        f"RegionMirror.MaximizeProbe.{os.getpid()}",
    )
    checked(register_class(c.byref(definition)))
    x, y, width, height = initial
    hwnd = checked(create_window(
        0, definition.name, "RegionMirror maximize probe - disposable test window",
        0x00CF0000, x, y, width, height, None, None, instance, None,
    ))
    show_window(hwnd, 5)  # SW_SHOW; standard style and normal DefWindowProc sizing.
    print(json.dumps({"hwnd": hwnd, "pid": os.getpid()}), flush=True)
    message = w.MSG()
    while True:
        received = get_message(c.byref(message), None, 0, 0)
        if received <= 0:
            return 0 if received == 0 else 1
        translate_message(c.byref(message))
        dispatch_message(c.byref(message))


def monitors():
    result = []

    @MONITORPROC
    def collect(handle, _dc, _rect, _data):
        info = MonitorInfo()
        info.size = c.sizeof(info)
        if get_monitor(handle, c.byref(info)):
            result.append({"device": info.device, "bounds": rect_values(info.monitor),
                           "work": rect_values(info.work), "primary": bool(info.flags & 1)})
        return True

    checked(enum_monitors(None, None, collect, 0))
    return sorted(result, key=lambda item: not item["primary"])


def run_case(label, monitor, target, asynchronous):
    left, top, right, bottom = monitor["work"]
    initial = [left + 80, top + 80, min(700, right - left - 160), min(450, bottom - top - 160)]
    process = subprocess.Popen(
        [sys.executable, str(Path(__file__).resolve()), "--child", json.dumps(initial)],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        creationflags=subprocess.CREATE_NO_WINDOW,
    )
    hwnd = None
    try:
        ready = queue.Queue()
        threading.Thread(target=lambda: ready.put(process.stdout.readline()), daemon=True).start()
        message = ready.get(timeout=5)
        if not message:
            raise RuntimeError("Test window failed to start: " + process.stderr.read())
        identity = json.loads(message)
        hwnd = identity["hwnd"]
        owner = w.DWORD()
        checked(get_owner(hwnd, c.byref(owner)))
        if owner.value != process.pid or process.pid == os.getpid():
            raise RuntimeError("The test must operate on its own separate child process.")
        baseline = settled(hwnd, lambda state: not state["isZoomed"])
        rounds = []
        for _ in range(2):
            command(hwnd, 0xF030)  # SC_MAXIMIZE: same command used by the standard maximize control.
            maximized = settled(hwnd, lambda state: state["isZoomed"])
            x1, y1, x2, y2 = target
            flags = 0x0004 | 0x0010 | (0x4000 if asynchronous else 0)  # NOZORDER | NOACTIVATE | ASYNC
            checked(set_position(hwnd, None, x1, y1, x2 - x1, y2 - y1, flags))
            resized = settled(hwnd, lambda state: state["outer"] == target)
            # GetWindowRect includes invisible frame margins. Test the ordinary
            # DWM-margin compensation separately, still without restoring first.
            if resized["visible"] is None:
                raise RuntimeError("DWM did not expose the visible window bounds.")
            outer, visible = resized["outer"], resized["visible"]
            compensated_target = [
                x1 - (visible[0] - outer[0]), y1 - (visible[1] - outer[1]),
                x2 + (outer[2] - visible[2]), y2 + (outer[3] - visible[3]),
            ]
            cx1, cy1, cx2, cy2 = compensated_target
            checked(set_position(hwnd, None, cx1, cy1, cx2 - cx1, cy2 - cy1, flags))
            compensated = settled(hwnd, lambda state: state["visible"] == target)
            command(hwnd, 0xF120)  # SC_RESTORE; no manual restoration of saved coordinates.
            restored = settled(hwnd, lambda state: not state["isZoomed"] and state["outer"] == baseline["outer"])
            checks = {
                "initialMaximizeSucceeded": maximized["isZoomed"],
                "targetOuterReached": resized["outer"] == target,
                "isZoomedPreserved": resized["isZoomed"],
                "styleMaximizedPreserved": resized["styleMaximized"],
                "showCmdMaximizedPreserved": resized["showCmd"] == 3,
                "normalPlacementPreserved": resized["normalPlacement"] == baseline["normalPlacement"],
                "compensatedVisibleBoundsReached": compensated["visible"] == target,
                "compensatedMaximizeStatePreserved": compensated["isZoomed"] and compensated["styleMaximized"] and compensated["showCmd"] == 3,
                "compensatedNormalPlacementPreserved": compensated["normalPlacement"] == baseline["normalPlacement"],
                "nativeRestoreClearedMaximize": not restored["isZoomed"] and not restored["styleMaximized"] and restored["showCmd"] == 1,
                "nativeRestoreReturnedToOriginal": restored["outer"] == baseline["outer"],
            }
            rounds.append({"maximized": maximized, "resized": resized, "compensatedTarget": compensated_target,
                           "compensated": compensated, "restored": restored,
                           "checks": checks, "passed": all(checks.values())})
        return {"case": label, "asynchronous": asynchronous, "controllerPid": os.getpid(),
                "windowPid": process.pid, "target": target, "baseline": baseline,
                "rounds": rounds, "passed": all(item["passed"] for item in rounds)}
    finally:
        if hwnd and process.poll() is None:
            output = c.c_size_t()
            send_timeout(hwnd, 0x0010, 0, 0, 0x0002, 2000, c.byref(output))  # WM_CLOSE
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=3)
        process.stdout.close()
        process.stderr.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--child", help=argparse.SUPPRESS)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    checked(set_dpi(c.c_void_p(-4)))  # Per-monitor v2 in both independent processes.
    if args.child:
        return child(json.loads(args.child))
    if not args.report:
        parser.error("--report is required")
    screens = monitors()
    cases = []
    for index, monitor in enumerate(screens[:2]):
        left, top, right, bottom = monitor["work"]
        cases.append((f"monitor-{index + 1}", monitor, [
            left + 120, top + 100, min(right - 80, left + 1080), min(bottom - 60, top + 640),
        ]))
    if len(screens) >= 2:
        a, b = screens[:2]
        for left, right in [(a, b), (b, a)]:
            edge = left["bounds"][2]
            overlap_top = max(left["bounds"][1], right["bounds"][1])
            overlap_bottom = min(left["bounds"][3], right["bounds"][3])
            if edge == right["bounds"][0] and overlap_bottom - overlap_top >= 360:
                cases.append(("cross-monitor", a, [edge - 480, overlap_top + 40, edge + 480, overlap_top + 360]))
                break
    evidence = {
        "windowsVersion": str(sys.getwindowsversion()),
        "pointerBits": c.sizeof(c.c_void_p) * 8,
        "method": "Separate processes; standard WS_OVERLAPPEDWINDOW/DefWindowProc; SC_MAXIMIZE -> external SetWindowPos -> SC_RESTORE.",
        "monitors": screens, "cases": [],
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    try:
        for label, monitor, target in cases:
            for asynchronous in (False, True):
                result = run_case(label, monitor, target, asynchronous)
                evidence["cases"].append(result)
                print(f"{label}, async={asynchronous}: {'PASS' if result['passed'] else 'FAIL'}", flush=True)
        evidence["passed"] = bool(cases) and all(case["passed"] for case in evidence["cases"])
    finally:
        args.report.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if evidence["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
