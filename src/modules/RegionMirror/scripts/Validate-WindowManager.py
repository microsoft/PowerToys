# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

"""Exercise the actual RegionMirror window manager against disposable Win32 windows.

The application must already be built. This test uses --window-manager-only;
it does not create a virtual display, install a driver, or start screen capture.
Drag boundaries are explicitly synthetic: each owned child emits NotifyWinEvent
for MOVESIZESTART/END while its real position and the pointer are controlled.
Maximize/restore use native SC_MAXIMIZE/SC_RESTORE. All maximize LOCATIONCHANGE
events and region-fitting SetWindowPos calls come from Windows/the real manager.
"""

import argparse
import ctypes as c
from ctypes import wintypes as w
import importlib.util
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import threading
import time


SCRIPT = Path(__file__).resolve()
MODULE = SCRIPT.parent.parent
spec = importlib.util.spec_from_file_location("region_mirror_maximize_probe", SCRIPT.with_name("Probe-MaximizedWindow.py"))
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)

WM_CLOSE = 0x0010
WM_GETTEXT = 0x000D
WM_DRAG_START = 0x8051
WM_DRAG_END = 0x8052
SC_MAXIMIZE = 0xF030
SC_RESTORE = 0xF120
EVENT_SYSTEM_MOVESIZESTART = 0x000A
EVENT_SYSTEM_MOVESIZEEND = 0x000B
ENUMPROC = c.WINFUNCTYPE(w.BOOL, w.HWND, probe.LPARAM)
notify_event = probe.api(probe.user32, "NotifyWinEvent", None, w.DWORD, w.HWND, w.LONG, w.LONG)
enum_windows = probe.api(probe.user32, "EnumWindows", w.BOOL, ENUMPROC, probe.LPARAM)
enum_children = probe.api(probe.user32, "EnumChildWindows", w.BOOL, w.HWND, ENUMPROC, probe.LPARAM)
get_cursor = probe.api(probe.user32, "GetCursorPos", w.BOOL, c.POINTER(w.POINT))
set_cursor = probe.api(probe.user32, "SetCursorPos", w.BOOL, c.c_int, c.c_int)


def child(initial):
    """A standard window with only two diagnostic drag-boundary messages added."""
    @probe.WNDPROC
    def procedure(hwnd, message, wp, lp):
        if message in (WM_DRAG_START, WM_DRAG_END):
            event = EVENT_SYSTEM_MOVESIZESTART if message == WM_DRAG_START else EVENT_SYSTEM_MOVESIZEEND
            # Emit from the window's own process/UI thread, not the test controller.
            notify_event(event, hwnd, 0, 0)  # OBJID_WINDOW, CHILDID_SELF
            return 1
        if message == 0x0002:  # WM_DESTROY
            probe.post_quit(0)
            return 0
        return probe.default_proc(hwnd, message, wp, lp)

    instance = probe.module_handle(None)
    definition = probe.WNDCLASS(
        0, procedure, 0, 0, instance, None, None, 6, None,
        f"RegionMirror.WindowManagerValidation.{os.getpid()}",
    )
    probe.checked(probe.register_class(c.byref(definition)))
    x, y, width, height = initial
    hwnd = probe.checked(probe.create_window(
        0, definition.name, "RegionMirror window-manager validation - disposable child",
        0x00CF0000, x, y, width, height, None, None, instance, None,
    ))
    probe.show_window(hwnd, 4)  # SW_SHOWNOACTIVATE; ordinary WS_OVERLAPPEDWINDOW.
    print(json.dumps({"hwnd": hwnd, "pid": os.getpid()}), flush=True)
    message = w.MSG()
    while True:
        received = probe.get_message(c.byref(message), None, 0, 0)
        if received <= 0:
            return 0 if received == 0 else 1
        probe.translate_message(c.byref(message))
        probe.dispatch_message(c.byref(message))


def require_owned(hwnd, process):
    if process.poll() is not None:
        raise RuntimeError(f"Owned process {process.pid} exited before the operation.")
    owner = w.DWORD()
    probe.checked(probe.get_owner(hwnd, c.byref(owner)))
    if owner.value != process.pid or process.pid == os.getpid():
        raise RuntimeError("Refusing to operate on a window outside the owned child process.")


def send_owned(hwnd, process, message):
    require_owned(hwnd, process)
    result = c.c_size_t()
    probe.checked(probe.send_timeout(hwnd, message, 0, 0, 0x0002, 2000, c.byref(result)))
    return result.value


def system_command(window, command):
    require_owned(window.hwnd, window.process)
    probe.command(window.hwnd, command)


def wait_state(window, predicate, description, timeout=5.0):
    require_owned(window.hwnd, window.process)
    state = probe.settled(window.hwnd, predicate, timeout=timeout)
    if not predicate(state):
        raise RuntimeError(f"{description}; final state: {json.dumps(state)}")
    return state


class OwnedWindow:
    def __init__(self, initial, label):
        self.label = label
        self.hwnd = None
        self.process = subprocess.Popen(
            [sys.executable, str(SCRIPT), "--child", json.dumps(initial)],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )
        try:
            ready = queue.Queue()
            threading.Thread(target=lambda: ready.put(self.process.stdout.readline()), daemon=True).start()
            try:
                message = ready.get(timeout=5)
            except queue.Empty as error:
                raise RuntimeError(f"Timed out waiting for disposable {label} window readiness.") from error
            if not message:
                raise RuntimeError(f"Disposable {label} window did not report readiness.")
            identity = json.loads(message)
            if identity["pid"] != self.process.pid:
                raise RuntimeError("Child readiness contained a different process identity.")
            self.hwnd = int(identity["hwnd"])
            require_owned(self.hwnd, self.process)
        except BaseException:
            self.close()
            raise

    def close(self):
        if self.process.poll() is None:
            if self.hwnd:
                try:
                    send_owned(self.hwnd, self.process, WM_CLOSE)
                except Exception:
                    pass
            try:
                self.process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                self.process.terminate()  # Only this Popen-owned disposable child.
                self.process.wait(timeout=3)
        if self.process.stdout:
            self.process.stdout.close()
        if self.process.stderr:
            self.process.stderr.close()


def owned_top_windows(process):
    if process.poll() is not None:
        return []
    windows = []

    @ENUMPROC
    def collect(hwnd, _data):
        owner = w.DWORD()
        if probe.get_owner(hwnd, c.byref(owner)) and owner.value == process.pid:
            windows.append(hwnd)
        return True

    probe.checked(enum_windows(collect, 0))
    return windows


def owned_text(hwnd, process):
    require_owned(hwnd, process)
    buffer = c.create_unicode_buffer(4096)
    result = c.c_size_t()
    if not probe.send_timeout(hwnd, WM_GETTEXT, len(buffer), c.cast(buffer, c.c_void_p).value,
                              0x0002, 250, c.byref(result)):
        return ""
    return buffer.value


def wait_manager_ready(process, timeout=10.0):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f"Manager application exited before readiness (code {process.returncode}).")
        for hwnd in owned_top_windows(process):
            if owned_text(hwnd, process) != "Region Mirror - local PoC":
                continue
            controls = []

            @ENUMPROC
            def collect(child_hwnd, _data):
                controls.append(child_hwnd)
                return True

            enum_children(hwnd, collect, 0)
            for control in controls:
                if "Window manager active" in owned_text(control, process):
                    return hwnd
        time.sleep(0.05)
    raise RuntimeError("Manager readiness text 'Window manager active' did not appear.")


def wait_managed_count(process, hwnd, expected, timeout=5.0):
    deadline = time.monotonic() + timeout
    last_status = ""
    while time.monotonic() < deadline:
        require_owned(hwnd, process)
        controls = []

        @ENUMPROC
        def collect(child_hwnd, _data):
            controls.append(child_hwnd)
            return True

        enum_children(hwnd, collect, 0)
        for control in controls:
            text = owned_text(control, process)
            if "Window manager active" not in text:
                continue
            last_status = text
            count = re.search(r"\bManaged:\s*(\d+)\b", text)
            if count and int(count.group(1)) == expected:
                return text
        time.sleep(0.05)
    raise RuntimeError(f"Manager did not report Managed: {expected}; last status: {last_status!r}")


def close_manager(process, hwnd):
    if process.poll() is None:
        if hwnd:
            try:
                send_owned(hwnd, process, WM_CLOSE)
            except Exception:
                # A stale HWND or hung window must not prevent cleanup of our own
                # Popen process. Never redirect the close to any other window.
                pass
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.terminate()  # Only the diagnostic application started by this test.
            process.wait(timeout=3)
            raise RuntimeError("Manager application did not exit after WM_CLOSE; its owned process was terminated.")


def move_normal(window, outer):
    require_owned(window.hwnd, window.process)
    if probe.is_zoomed(window.hwnd):
        raise RuntimeError("The test never calls SetWindowPos on a maximized window; fitting belongs to the real manager.")
    left, top, right, bottom = outer
    probe.checked(probe.set_position(window.hwnd, None, left, top, right - left, bottom - top, 0x0004 | 0x0010))
    return wait_state(window, lambda state: not state["isZoomed"] and state["outer"] == outer,
                      "Ordinary normal-window move/resize was unexpectedly changed")


class PointerControl:
    def __init__(self):
        point = w.POINT()
        probe.checked(get_cursor(c.byref(point)))
        self.original = (point.x, point.y)
        self.last = None

    def place(self, point):
        probe.checked(set_cursor(*point))
        self.last = tuple(point)
        actual = w.POINT()
        probe.checked(get_cursor(c.byref(actual)))
        if (actual.x, actual.y) != self.last:
            raise RuntimeError("The pointer could not reach the requested test coordinate.")

    def restore_if_unchanged(self):
        current = w.POINT()
        if self.last and get_cursor(c.byref(current)) and (current.x, current.y) == self.last:
            set_cursor(*self.original)


def drag_boundary(window, outer, pointer, cursor, evidence, manager, manager_hwnd, expected_count):
    start = probe.snapshot(window.hwnd)
    cursor.place((start["outer"][0] + 40, start["outer"][1] + 12))
    if send_owned(window.hwnd, window.process, WM_DRAG_START) != 1:
        raise RuntimeError("Child did not acknowledge the synthetic drag-start stimulus.")
    cursor.place(pointer)
    moved = move_normal(window, outer)
    if send_owned(window.hwnd, window.process, WM_DRAG_END) != 1:
        raise RuntimeError("Child did not acknowledge the synthetic drag-end stimulus.")
    # OUTOFCONTEXT drag-end must be handled while this window is still normal.
    # Await the real manager's count before issuing native maximize; no fixed sleep.
    manager_status = wait_managed_count(manager, manager_hwnd, expected_count)
    evidence.append({
        "kind": "synthetic-child-NotifyWinEvent-MOVESIZESTART-END",
        "originPid": window.process.pid, "hwnd": window.hwnd,
        "pointerAtEnd": list(pointer), "normalWindowAfterMove": moved,
        "managerStatusAfterBoundary": manager_status,
        "note": "Controlled drag boundaries, not an actual mouse drag. No LOCATIONCHANGE event is emitted by the test.",
    })
    return moved


def native_round(window, expected_visible, original, label):
    system_command(window, SC_MAXIMIZE)
    maximized = wait_state(
        window,
        lambda state: state["isZoomed"] and state["visible"] == expected_visible,
        f"{label}: native maximize did not reach the expected visible bounds",
    )
    checks = {
        "visibleBounds": maximized["visible"] == expected_visible,
        "isZoomed": maximized["isZoomed"],
        "styleMaximized": maximized["styleMaximized"],
        "showCmdMaximized": maximized["showCmd"] == 3,
        "normalPlacementPreserved": maximized["normalPlacement"] == original["normalPlacement"],
    }
    system_command(window, SC_RESTORE)
    restored = wait_state(
        window,
        lambda state: not state["isZoomed"] and state["outer"] == original["outer"],
        f"{label}: raw SC_RESTORE did not restore the original normal rectangle",
    )
    checks["nativeRestore"] = not restored["styleMaximized"] and restored["showCmd"] == 1
    checks["originalOuterRestored"] = restored["outer"] == original["outer"]
    if not all(checks.values()):
        raise RuntimeError(f"{label}: failed state checks {json.dumps(checks)}")
    return {"case": label, "maximized": maximized, "restored": restored, "checks": checks, "passed": True}


def run(args):
    executable = args.executable.resolve()
    report_path = args.report.resolve()
    output = report_path.parent
    if not executable.is_file():
        raise RuntimeError("Build the RegionMirror application before running this integration check.")
    output.mkdir(parents=True, exist_ok=True)
    manager_report = output / "manager-result.json"
    if manager_report.exists():
        manager_report.unlink()

    screens = probe.monitors()
    if not screens:
        raise RuntimeError("No active monitor is available for the disposable test windows.")
    monitor = screens[0]
    left, top, right, bottom = monitor["work"]
    work_width, work_height = right - left, bottom - top
    if work_width < 800 or work_height < 600:
        raise RuntimeError("This integration check needs an active work area of at least 800 x 600 physical pixels.")
    region_width = min(960, (work_width - 144) // 2)
    region_height = min(540, work_height - 144)
    region = [left + 48, top + 48, left + 48 + region_width, top + 48 + region_height]
    normal_width = min(420, region_width - 48)
    normal_height = min(280, region_height - 48)
    initial = [region[0] + 24, region[1] + 24, normal_width, normal_height]
    inside = [region[0] + 30, region[1] + 30, region[0] + 30 + normal_width, region[1] + 30 + normal_height]
    moved_inside = [inside[0] + 8, inside[1] + 8, inside[2] - 8, inside[3] - 8]
    outside = [right - 48 - normal_width, top + 72, right - 48, top + 72 + normal_height]
    if outside[0] < region[2]:
        raise RuntimeError("The test layout does not leave a separate drag-out area.")

    evidence = {
        "passed": False, "windowsVersion": str(sys.getwindowsversion()),
        "controllerPid": os.getpid(), "region": region, "monitor": monitor,
        "managerMode": "--window-manager-only (no driver, virtual display, or capture)",
        "dragStimulus": "Synthetic NotifyWinEvent MOVESIZESTART/END from each disposable child UI thread; real positions and pointer.",
        "maximizeStimulus": "Native SC_MAXIMIZE/SC_RESTORE; OS-generated LOCATIONCHANGE; actual RegionWindowManager performs all maximize fitting.",
        "testFitCalls": 0, "cases": [], "dragBoundaries": [], "ownedChildren": [],
    }
    cursor = PointerControl()
    children = []
    manager = None
    manager_hwnd = None
    manager_stopped = False
    logs = []
    try:
        unmanaged = OwnedWindow(initial, "unregistered")
        children.append(unmanaged)
        evidence["ownedChildren"].append({"label": unmanaged.label, "pid": unmanaged.process.pid, "hwnd": unmanaged.hwnd})
        baseline = wait_state(unmanaged, lambda state: not state["isZoomed"], "Child did not show normally")
        system_command(unmanaged, SC_MAXIMIZE)
        native = wait_state(unmanaged, lambda state: state["isZoomed"] and state["visible"] is not None,
                            "Could not calibrate native maximize before the manager started")
        if native["visible"] == region:
            raise RuntimeError("The test region must differ from the ordinary native maximize bounds.")
        system_command(unmanaged, SC_RESTORE)
        wait_state(unmanaged, lambda state: not state["isZoomed"] and state["outer"] == baseline["outer"],
                   "Calibration restore failed")
        evidence["nativeMaximizeWithoutManager"] = native

        logs = [open(output / "manager.stdout.log", "w", encoding="utf-8"),
                open(output / "manager.stderr.log", "w", encoding="utf-8")]
        region_argument = f"{region[0]},{region[1]},{region[2] - region[0]},{region[3] - region[1]}"
        manager = subprocess.Popen(
            [str(executable), "--window-manager-only", "--region", region_argument, "--report", str(manager_report)],
            stdout=logs[0], stderr=logs[1], creationflags=subprocess.CREATE_NO_WINDOW,
        )
        evidence["managerPid"] = manager.pid
        manager_hwnd = wait_manager_ready(manager)
        evidence["managerReady"] = True
        evidence["initialManagerStatus"] = wait_managed_count(manager, manager_hwnd, 0)
        evidence["cases"].append(native_round(unmanaged, native["visible"], baseline, "unregistered-maximize-unaffected"))

        managed = OwnedWindow(initial, "managed")
        children.append(managed)
        evidence["ownedChildren"].append({"label": managed.label, "pid": managed.process.pid, "hwnd": managed.hwnd})
        ordinary = move_normal(managed, inside)
        evidence["cases"].append({"case": "ordinary-unregistered-move-resize", "state": ordinary, "passed": True})
        drag_boundary(managed, inside, (inside[0] + 40, inside[1] + 12), cursor, evidence["dragBoundaries"], manager, manager_hwnd, 1)
        original = move_normal(managed, moved_inside)
        evidence["cases"].append({"case": "ordinary-enrolled-move-resize-unaffected", "state": original, "passed": True})
        for round_index in range(2):
            evidence["cases"].append(native_round(managed, region, original, f"enrolled-native-maximize-restore-{round_index + 1}"))

        dragged_out = drag_boundary(managed, outside, (outside[0] + 40, outside[1] + 12), cursor, evidence["dragBoundaries"], manager, manager_hwnd, 0)
        evidence["cases"].append(native_round(managed, native["visible"], dragged_out, "drag-out-unbinds-native-maximize"))

        re_enrolled = drag_boundary(managed, inside, (inside[0] + 40, inside[1] + 12), cursor, evidence["dragBoundaries"], manager, manager_hwnd, 1)
        evidence["cases"].append(native_round(managed, region, re_enrolled, "re-enrolled-before-manager-stop"))
        close_manager(manager, manager_hwnd)
        manager_stopped = True
        if manager.returncode != 0:
            raise RuntimeError(f"Manager diagnostic exited with code {manager.returncode}.")
        evidence["cases"].append(native_round(managed, native["visible"], re_enrolled, "after-manager-stop-no-adjustments"))
        if not manager_report.is_file():
            raise RuntimeError("Manager diagnostic did not write its final JSON report.")
        application_report = json.loads(manager_report.read_text(encoding="utf-8-sig"))
        status = application_report.get("windowManager")
        if not isinstance(status, dict):
            raise RuntimeError("Application report is missing windowManager diagnostics.")
        if status.get("error"):
            raise RuntimeError(f"Manager reported an adjustment error: {status['error']}")
        if status.get("managedCount") != 1 or status.get("adjustedCount", 0) < 3:
            raise RuntimeError(f"Unexpected manager activity counters: {json.dumps(status)}")
        if status.get("active") is True:
            raise RuntimeError("Manager report still claims hooks are active after Stop.")
        evidence["managerReport"] = application_report
        evidence["passed"] = all(case["passed"] for case in evidence["cases"])
    except BaseException as error:
        evidence["error"] = f"{type(error).__name__}: {error}"
    finally:
        cleanup_errors = []
        if manager and not manager_stopped:
            try:
                close_manager(manager, manager_hwnd)
            except Exception as error:
                cleanup_errors.append(str(error))
        for window in reversed(children):
            try:
                window.close()
            except Exception as error:
                cleanup_errors.append(str(error))
        cursor.restore_if_unchanged()
        for log in logs:
            log.close()
        evidence["cleanup"] = {
            "managerExited": manager is None or manager.poll() is not None,
            "allDisposableChildrenExited": all(window.process.poll() is not None for window in children),
            "errors": cleanup_errors,
        }
        if cleanup_errors:
            evidence["passed"] = False
        if manager_report.is_file() and "managerReport" not in evidence:
            try:
                evidence["managerReport"] = json.loads(manager_report.read_text(encoding="utf-8-sig"))
            except Exception as error:
                evidence["managerReportReadError"] = str(error)
        report_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Window manager integration: {'PASS' if evidence['passed'] else 'FAIL'}\nEvidence: {report_path}")
    return 0 if evidence["passed"] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--child", help=argparse.SUPPRESS)
    parser.add_argument("--executable", type=Path, default=MODULE / "bin/x64/Debug/PowerToys.RegionMirror.exe")
    parser.add_argument("--report", type=Path, default=MODULE / "artifacts/window-manager-validation/result.json")
    args = parser.parse_args()
    probe.checked(probe.set_dpi(c.c_void_p(-4)))
    if args.child:
        return child(json.loads(args.child))
    return run(args)


if __name__ == "__main__":
    sys.exit(main())
