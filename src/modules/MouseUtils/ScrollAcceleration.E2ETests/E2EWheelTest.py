# End-to-end wheel test for Scroll Acceleration (manual, Windows only).
#
# Needs an exe built with -DSCROLLACCELERATION_TEST_ACCEPT_INJECTED (the shipping build ignores injected input):
#   $env:CL = "-DSCROLLACCELERATION_TEST_ACCEPT_INJECTED"; msbuild ScrollAcceleration.vcxproj -p:Configuration=Release -p:Platform=x64 -p:OutDir=<dir># Run:  python E2EWheelTest.py <path to that PowerToys.ScrollAcceleration.exe>
# It installs its own low-level hook, injects wheel ticks and prints what an application would receive.
# Note: the injected ticks also scroll whatever window is under the cursor.
# Expected with default settings: fast ticks -> 120,180,240,...; slow ticks and a direction change -> reset.
import ctypes, ctypes.wintypes as wt, subprocess, sys, threading, time, os

exe = sys.argv[1]
u32 = ctypes.WinDLL('user32', use_last_error=True)
k32 = ctypes.WinDLL('kernel32')

WH_MOUSE_LL = 14
WM_MOUSEWHEEL = 0x020A
LLMHF_INJECTED = 1
HOOKPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, ctypes.c_int, wt.WPARAM, wt.LPARAM)

class MSLL(ctypes.Structure):
    _fields_ = [('pt', wt.POINT), ('mouseData', wt.DWORD), ('flags', wt.DWORD), ('time', wt.DWORD), ('dwExtraInfo', ctypes.c_size_t)]

u32.SetWindowsHookExW.argtypes = [ctypes.c_int, HOOKPROC, wt.HINSTANCE, wt.DWORD]
u32.SetWindowsHookExW.restype = wt.HHOOK
u32.CallNextHookEx.argtypes = [wt.HHOOK, ctypes.c_int, wt.WPARAM, wt.LPARAM]
u32.CallNextHookEx.restype = ctypes.c_ssize_t

seen = []

def proc(n, w, l):
    if n == 0 and w == WM_MOUSEWHEEL:
        info = ctypes.cast(l, ctypes.POINTER(MSLL)).contents
        delta = ctypes.c_short(info.mouseData >> 16).value
        seen.append((delta, info.dwExtraInfo))
    return u32.CallNextHookEx(None, n, w, l)

cb = HOOKPROC(proc)
hook = u32.SetWindowsHookExW(WH_MOUSE_LL, cb, None, 0)  # installed first -> runs after the module's hook
assert hook, 'listener hook failed'

class MOUSEINPUT(ctypes.Structure):
    _fields_ = [('dx', wt.LONG), ('dy', wt.LONG), ('mouseData', wt.DWORD), ('dwFlags', wt.DWORD), ('time', wt.DWORD), ('dwExtraInfo', ctypes.c_size_t)]

class INPUT(ctypes.Structure):
    class _U(ctypes.Union):
        _fields_ = [('mi', MOUSEINPUT)]
    _anonymous_ = ('u',)
    _fields_ = [('type', wt.DWORD), ('u', _U)]

def wheel(delta):
    i = INPUT(type=0)
    i.mi.mouseData = ctypes.c_uint32(delta & 0xFFFFFFFF).value
    i.mi.dwFlags = 0x0800
    u32.SendInput(1, ctypes.byref(i), ctypes.sizeof(INPUT))

def pump(seconds):
    end = time.time() + seconds
    msg = wt.MSG()
    while time.time() < end:
        while u32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
            u32.TranslateMessage(ctypes.byref(msg)); u32.DispatchMessageW(ctypes.byref(msg))
        time.sleep(0.005)

def burst(deltas, gap):
    seen.clear()
    def run():
        for d in deltas:
            wheel(d); time.sleep(gap)
    t = threading.Thread(target=run); t.start()
    pump(len(deltas) * gap + 1.0)
    t.join()
    return list(seen)

# Baseline without the module: events pass through unchanged.
base = burst([120] * 3, 0.03)
print('baseline (no module):      ', base)

p = subprocess.Popen([exe, str(os.getpid())])
pump(1.5)
assert p.poll() is None, 'module exited early'

fast = burst([120] * 6, 0.03)
print('fast ticks (30 ms apart):  ', fast)
slow = burst([120] * 3, 0.30)
print('slow ticks (300 ms apart): ', slow)
rev = burst([120, 120, -120, -120], 0.03)
print('direction reversal:        ', rev)

p.terminate(); p.wait(5)
after = burst([120] * 3, 0.03)
print('after module stopped:      ', after)
u32.UnhookWindowsHookEx(hook)
