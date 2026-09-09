// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class NotepadInputWindow : IKeyboardInputWindow, IDisposable
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNotTopmost = new(-2);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private const int SwRestore = 9;

    private static readonly bool IsModernNotepad = Directory.Exists(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages",
        "Microsoft.WindowsNotepad_8wekyb3d8bbwe"));

    public const string ProcessName = "notepad";

    private readonly string rootDirectory;
    private readonly string fileName;
    private readonly Session window;
    private bool disposed;

    public NotepadInputWindow(string initialText = "")
    {
        rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "PowerToys-KeyboardManager-UITests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootDirectory);
        fileName = $"KeyboardManager-UITest-{Guid.NewGuid():N}.txt";
        FilePath = Path.Combine(rootDirectory, fileName);
        File.WriteAllText(FilePath, initialText);

        Session? launchedWindow = null;
        for (var attempt = 1; attempt <= 2 && launchedWindow is null; attempt++)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{FilePath}\"",
                UseShellExecute = true,
            });
            Assert.IsNotNull(process, "System Notepad could not be started.");

            launchedWindow = WindowsFinder.WaitForWindowByApp(
                ProcessName,
                IsDocumentWindow,
                timeoutMS: 45_000);
        }

        window = launchedWindow ?? throw new AssertFailedException($"Notepad did not open '{fileName}' after two attempts.");

        Assert.IsTrue(FocusInput(), "Notepad did not own foreground after opening the test file.");
    }

    public string FilePath { get; }

    public IntPtr Handle => new(window.WindowHandle);

    public string Text
    {
        get
        {
            SaveDocument();
            return ReadFile();
        }
    }

    public bool IsOpen => WindowsFinder.ListByApp(ProcessName).Any(IsDocumentWindow);

    public bool FocusInput(int timeoutMS = 10_000)
    {
        ShowWindow(Handle, SwRestore);
        SetTopmost(topmost: true);
        try
        {
            window.EnsureForeground();
            var editor = FindEditorWindow();
            if (editor != IntPtr.Zero && GetWindowRect(editor, out var editorBounds))
            {
                MouseHelper.LeftClickAt(
                    editorBounds.Left + ((editorBounds.Right - editorBounds.Left) / 2),
                    editorBounds.Top + ((editorBounds.Bottom - editorBounds.Top) / 2));
            }
            else if (GetWindowRect(Handle, out var windowBounds))
            {
                MouseHelper.LeftClickAt(
                    windowBounds.Left + ((windowBounds.Right - windowBounds.Left) / 2),
                    windowBounds.Top + ((windowBounds.Bottom - windowBounds.Top) / 2));
            }

            if (!WindowControl.WaitForForeground(Handle, timeoutMS, requiredConsecutiveMatches: 2))
            {
                return false;
            }

            if (!IsBoundDocumentActive())
            {
                return false;
            }
        }
        finally
        {
            SetTopmost(topmost: false);
        }

        SendControlChord(Key.End);
        return WindowControl.WaitForForeground(Handle, timeoutMS: 2_000, requiredConsecutiveMatches: 2) &&
            IsBoundDocumentActive();
    }

    public bool WaitForText(string expected, int timeoutMS)
    {
        SaveDocument();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            if (ReadFile().Equals(expected, StringComparison.Ordinal))
            {
                return true;
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return ReadFile().Equals(expected, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (IsOpen)
        {
            try
            {
                if (FocusInput(timeoutMS: 3_000))
                {
                    SendControlChord(Key.S);
                    Thread.Sleep(300);
                    if (IsModernNotepad)
                    {
                        if (FocusInput(timeoutMS: 3_000))
                        {
                            SendControlChord(Key.W);
                            WaitForDocumentToClose(timeoutMS: 3_000);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        if (IsOpen && !IsModernNotepad)
        {
            WindowControl.TryCloseByApp(
                ProcessName,
                candidate => candidate.Hwnd == window.WindowHandle,
                timeoutMS: 3_000);
        }

        if (!IsOpen)
        {
            try
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public void SaveDocument()
    {
        Assert.IsTrue(FocusInput(), "Notepad did not own foreground before saving the test file.");
        SendControlChord(Key.S);
        Thread.Sleep(300);
    }

    private string ReadFile()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return File.ReadAllText(FilePath);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }

        return File.ReadAllText(FilePath);
    }

    private IntPtr FindEditorWindow()
    {
        var editor = IntPtr.Zero;
        EnumChildWindows(
            Handle,
            (child, _) =>
            {
                var className = GetWindowClassName(child);
                if (className.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                    className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase))
                {
                    editor = child;
                    return false;
                }

                return true;
            },
            IntPtr.Zero);
        return editor;
    }

    private bool IsDocumentWindow(WindowsFinder.WindowInfo candidate)
        => candidate.Title.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
            candidate.Title.Contains($"{Path.GetFileNameWithoutExtension(fileName)} - Notepad", StringComparison.OrdinalIgnoreCase);

    private bool IsBoundDocumentActive() => WindowsFinder.ListByApp(ProcessName).Any(candidate =>
        candidate.Hwnd == window.WindowHandle && IsDocumentWindow(candidate));

    private bool WaitForDocumentToClose(int timeoutMS)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            if (!IsOpen)
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return !IsOpen;
    }

    private static void SendControlChord(Key action)
    {
        try
        {
            KeyboardHelper.PressKey(Key.Ctrl);
            Thread.Sleep(50);
            KeyboardHelper.SendKey(action);
        }
        finally
        {
            KeyboardHelper.ReleaseKey(Key.Ctrl);
        }
    }

    private static string GetWindowClassName(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        var length = GetClassName(window, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : string.Empty;
    }

    private void SetTopmost(bool topmost)
    {
        SetWindowPos(
            Handle,
            topmost ? HwndTopmost : HwndNotTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpShowWindow);
    }

    private delegate bool EnumChildProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}

internal interface IKeyboardInputWindow
{
    bool FocusInput(int timeoutMS = 10_000);
}
