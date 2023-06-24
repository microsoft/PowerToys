// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    public partial class FormHelper : System.Windows.Forms.Form
    {
        private readonly List<FocusArea> focusZone = new();
        private readonly object bmScreenLock = new();
        private long lastClipboardEventTime;

        private IClipboardHelper remoteClipboardHelper;
        private const string TEXT_TYPE_SEP = "{4CFF57F7-BEDD-43d5-AE8F-27A61E886F2F}";

        private const int MAX_TEXT_SIZE = 20 * 1024 * 1024;
        private const int MAX_IMAGE_SIZE = 50 * 1024 * 1024;

        private struct FocusArea
        {
            internal RectangleF Rec;
            internal Color Color;

            internal FocusArea(RectangleF rec, Color color)
            {
                Rec = rec;
                Color = color;
            }
        }

        private Point focus1 = Point.Empty;
        private Point focus2 = Point.Empty;

        public FormHelper()
        {
            remoteClipboardHelper = IpcHelper.CreateIpcClient();

            if (remoteClipboardHelper == null)
            {
                QuitDueToCommunicationError();
                return;
            }

            SetDPIAwareness();
            InitializeComponent();
            lastClipboardEventTime = GetTick();
            ClipboardMMHelper.HookClipboard(this);
        }

        private void SetDPIAwareness()
        {
            int setProcessDpiAwarenessResult = -1;

            try
            {
                setProcessDpiAwarenessResult = NativeMethods.SetProcessDpiAwareness(2);
                SendLog(string.Format(CultureInfo.InvariantCulture, "SetProcessDpiAwareness: {0}.", setProcessDpiAwarenessResult));
            }
            catch (DllNotFoundException)
            {
                SendLog("SetProcessDpiAwareness is unsupported in Windows 7 and lower.");
            }
            catch (Exception e)
            {
                SendLog(e.ToString());
            }

            try
            {
                if (setProcessDpiAwarenessResult != 0)
                {
                    SendLog(string.Format(CultureInfo.InvariantCulture, "SetProcessDPIAware: {0}.", NativeMethods.SetProcessDPIAware()));
                }
            }
            catch (Exception e)
            {
                SendLog(e.ToString());
            }
        }

        private bool quitDueToCommunicationError;

        private void QuitDueToCommunicationError()
        {
            if (!quitDueToCommunicationError)
            {
                quitDueToCommunicationError = true;

                if (Process.GetCurrentProcess().SessionId != NativeMethods.WTSGetActiveConsoleSessionId())
                {
                    Logger.LogEvent(Application.ProductName + " cannot be used in a remote desktop or virtual machine session.");
                }
                else
                {
                    _ = MessageBox.Show(
                        "Unable to connect to Mouse Without Borders process, clipboard sharing is no longer working.\r\nSee EventLog for more information.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                Process.GetCurrentProcess().Kill();
            }
        }

        private void FormHelper_DragEnter(object sender, DragEventArgs e)
        {
            object o = e.Data.GetData(DataFormats.FileDrop);

            if (o != null)
            {
                e.Effect = DragDropEffects.Copy;
                Array ar = (string[])o;

                if (ar.Length > 0)
                {
                    string fileName = ar.GetValue(0).ToString();
                    Hide();

                    try
                    {
                        remoteClipboardHelper.SendDragFile(fileName);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogEvent("FormHelper_DragEnter: " + ex.Message, EventLogEntryType.Error);
                        QuitDueToCommunicationError();
                    }
                }
            }
        }

        private void TimerHelper_Tick(object sender, EventArgs e)
        {
            lock (bmScreenLock)
            {
                if (picScr == null)
                {
                    timerHelper.Stop();
                    Hide();
                }
            }
        }

        private void FormHelper_Shown(object sender, EventArgs e)
        {
            timerHelper.Start(); // To be sure.
            Hide();
        }

        internal void SendLog(string log)
        {
            try
            {
                Logger.LogEvent(log, EventLogEntryType.Warning);
            }
            catch (Exception e)
            {
                Logger.LogEvent(log + " ==> SendLog Exception: " + e.Message, EventLogEntryType.Warning);
            }
        }

        private long GetTick() // ms
        {
            return DateTime.Now.Ticks / 10000;
        }

        private string GetClipboardText()
        {
            string st = string.Empty, tmp = ClipboardMMHelper.GetText(TextDataFormat.UnicodeText);
            int txtL = 0, rtfL = 0, htmL = 0;

            if (tmp != null && (txtL = tmp.Length) > 0)
            {
                st += "TXT" + tmp + TEXT_TYPE_SEP;
            }

            tmp = ClipboardMMHelper.GetText(TextDataFormat.Rtf);

            if (tmp != null && (rtfL = tmp.Length) > 0)
            {
                st += "RTF" + tmp + TEXT_TYPE_SEP;
            }

            tmp = ClipboardMMHelper.GetText(TextDataFormat.Html);

            if (tmp != null && (htmL = tmp.Length) > 0)
            {
                st += "HTM" + tmp + TEXT_TYPE_SEP;
            }

            if (st.Length > 0)
            {
                if (st.Length > MAX_TEXT_SIZE)
                {
                    st = null;

                    SendLog(string.Format(CultureInfo.CurrentCulture, "GetClipboardText, Text too big: TXT = {0}, RTF = {1}, HTM = {2}.", txtL, rtfL, htmL));
                }
                else
                {
                    SendLog(string.Format(CultureInfo.CurrentCulture, "GetClipboardText: TXT = {0}, RTF = {1}, HTM = {2}.", txtL, rtfL, htmL));
                }
            }

            return st;
        }

#pragma warning disable CA2213 // Disposing is done by ComponentResourceManager
        private Image bmScreen;
        private Point startOrg;
        private Point start;
        private Point stop;
        private PictureBox left;
        private PictureBox top;
        private PictureBox right;
        private PictureBox bottom;
        private PictureBox picScr;
#pragma warning restore CA2213
        private int customScreenCaptureInProgress;
        private int screenLeft;
        private int screenTop;

        protected override void WndProc(ref Message m)
        {
            const int WM_DRAWCLIPBOARD = 0x0308;
            const int WM_CHANGECBCHAIN = 0x030D;
            const int WM_CLIPBOARDUPDATE = 0x031D;

            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                case WM_CLIPBOARDUPDATE:
                    ClipboardMMHelper.PassMessageToTheNextViewer(m);

                    if (GetTick() - lastClipboardEventTime < 1000)
                    {
                        lastClipboardEventTime = GetTick();
                        return;
                    }

                    if (customScreenCaptureInProgress > 0)
                    {
                        // 10 secs timeout for a failed capture.
                        if (GetTick() - lastClipboardEventTime < 10000)
                        {
                            return;
                        }
                        else
                        {
                            customScreenCaptureInProgress = 0;
                        }
                    }

                    lastClipboardEventTime = GetTick();

                    ByteArrayOrString? data = null;
                    bool isFile = false;

                    if (ClipboardMMHelper.ContainsText())
                    {
                        data = GetClipboardText();
                    }
                    else if (ClipboardMMHelper.ContainsImage())
                    {
                        MemoryStream ms = new();
                        Image im = ClipboardMMHelper.GetImage();

                        if (im != null)
                        {
                            im.Save(ms, ImageFormat.Png);

                            if (ms.Length > 0)
                            {
                                if (ms.Length > MAX_IMAGE_SIZE)
                                {
                                    SendLog("Image from clipboard, image too big: " + ms.Length.ToString(CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    data = ms.GetBuffer();
                                    SendLog("Image from clipboard: " + ms.Length.ToString(CultureInfo.InvariantCulture));
                                }
                            }
                            else
                            {
                                SendLog("ClipboardMMHelper image is 0 in length.");
                            }
                        }
                        else
                        {
                            SendLog("ClipboardMMHelper image (GetImage) is null.");
                        }

                        ms.Dispose();
                    }
                    else if (ClipboardMMHelper.ContainsFileDropList())
                    {
                        StringCollection files = ClipboardMMHelper.GetFileDropList();

                        if (files != null)
                        {
                            if (files.Count > 0)
                            {
                                data = files[0];
                                isFile = true;

                                SendLog("File from clipboard: " + files[0]);
                            }
                            else
                            {
                                SendLog("GetFileDropList returned no file.");
                            }
                        }
                        else
                        {
                            SendLog("GetFileDropList returned null.");
                        }
                    }
                    else
                    {
                        SendLog("ClipboardMMHelper does not have text/image/file data.");
                        return;
                    }

                    if (data != null)
                    {
                        try
                        {
                            remoteClipboardHelper.SendClipboardData((ByteArrayOrString)data, isFile);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogEvent("WM_DRAWCLIPBOARD: " + ex.Message, EventLogEntryType.Error);
                            QuitDueToCommunicationError();
                        }

                        GC.Collect();
                    }
                    else
                    {
                        SendLog("Null clipboard data returned. See previous messages (if any) for more information.");
                    }

                    break;

                case WM_CHANGECBCHAIN:
                    if (!ClipboardMMHelper.UpdateNextClipboardViewer(m))
                    {
                        ClipboardMMHelper.PassMessageToTheNextViewer(m);
                    }

                    break;

                case 0x401:
                    screenLeft = 0;
                    screenTop = 0;

                    foreach (Screen s in Screen.AllScreens)
                    {
                        if (s.Bounds.Left < screenLeft)
                        {
                            screenLeft = s.Bounds.Left;
                        }

                        if (s.Bounds.Top < screenTop)
                        {
                            screenTop = s.Bounds.Top;
                        }
                    }

                    customScreenCaptureInProgress = 1;
                    lastClipboardEventTime = GetTick();
                    SendLog("**************************************************\r\nScreen capture triggered.");
                    m.Result = new IntPtr(1);
                    break;

                case 0x406:
                    if (m.Msg == 0x406)
                    {
                        lock (bmScreenLock)
                        {
                            bmScreen = null;

                            for (int i = 0; i < 30; i++)
                            {
                                Thread.Sleep(100);

                                if (ClipboardMMHelper.ContainsImage())
                                {
                                    bmScreen = ClipboardMMHelper.GetImage();
                                    customScreenCaptureInProgress = 1;
                                    break;
                                }
                            }

                            if (bmScreen == null)
                            {
                                customScreenCaptureInProgress = 0;
                                SendLog("No image found in the clipboard.");
                            }
                            else
                            {
                                Opacity = 1;
                                picScr = new PictureBox();
                                picScr.Dock = DockStyle.Fill;
                                picScr.SizeMode = PictureBoxSizeMode.StretchImage;
                                picScr.Image = bmScreen;
                                picScr.Refresh();
                                Controls.Add(picScr);
                                AssignEventHandlers(picScr);
                            }
                        }
                    }

                    MouseMoveHandler();
                    break;

                case 0x407:
                    Program.DotForm.SetPosition(m.WParam.ToInt32(), m.LParam.ToInt32());
                    Program.DotForm.TopMost = true;
                    Program.DotForm.Show();
                    Application.DoEvents();

                    // Simulate input to help bring to the foreground, as it doesn't seem to work in every case otherwise.
                    NativeMethods.INPUT input = new NativeMethods.INPUT { type = (int)NativeMethods.InputType.INPUT_MOUSE, mi = { } };
                    NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[] { input };
                    _ = NativeMethods.SendInput(1, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
                    m.Result = SetForeGround() ? new IntPtr(1) : IntPtr.Zero;
                    break;

                case 0x408:
                    Program.DotForm.Hide();
                    break;

                case 0x400:
                    m.Result = Handle;
                    break;

                case SharedConst.QUIT_CMD:
                    Process.GetCurrentProcess().Kill();
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private bool SetForeGround()
        {
            string logTag = nameof(SetForeGround);
            IntPtr foreGroundWindow = NativeMethods.GetForegroundWindow();

            if (foreGroundWindow == Program.DotForm.Handle)
            {
                SendLog($"{logTag}.Foreground window is already the dot form: {foreGroundWindow}.");
                return true;
            }

            Program.DotForm.Activate();

            bool setForegroundWindow = NativeMethods.SetForegroundWindow(Program.DotForm.Handle);
            SendLog($"{logTag}.{nameof(NativeMethods.SetForegroundWindow)}({Program.DotForm.Handle}) returned: {setForegroundWindow}.");

            return LogForeGroundWindow(logTag);
        }

        private bool LogForeGroundWindow(string logTag)
        {
            IntPtr foreGroundWindow = NativeMethods.GetForegroundWindow();

            if (foreGroundWindow == Program.DotForm.Handle)
            {
                SendLog($"{logTag}.Foreground window is now the dot form: {Program.DotForm.Handle}.");
                return true;
            }
            else
            {
                SendLog($"{logTag}.Foreground window is: [{foreGroundWindow}].");
                return false;
            }
        }

        private void AssignEventHandlers(PictureBox p)
        {
            p.MouseDown += new System.Windows.Forms.MouseEventHandler(FormHelper_MouseDown);
            p.MouseMove += new System.Windows.Forms.MouseEventHandler(FormHelper_MouseMove);
            p.MouseUp += new System.Windows.Forms.MouseEventHandler(FormHelper_MouseUp);
        }

        private void MouseMoveHandler()
        {
            lock (bmScreenLock)
            {
                if (bmScreen != null && Opacity == 1)
                {
                    focusZone.Clear();
                    TopMost = true;
                    Show();

                    startOrg = new Point(MousePosition.X - screenLeft, MousePosition.Y - screenTop);

                    if (left == null)
                    {
                        left = new PictureBox();
                        left.BackColor = Color.Red;
                        Controls.Add(left);
                        left.BringToFront();
                        AssignEventHandlers(left);
                    }

                    left.Left = startOrg.X;
                    left.Top = startOrg.Y;
                    left.Width = 2;
                    left.Height = 100;
                    left.Show();
                    left.Refresh();

                    if (top == null)
                    {
                        top = new PictureBox();
                        top.BackColor = Color.Red;
                        Controls.Add(top);
                        top.BringToFront();
                        AssignEventHandlers(top);
                    }

                    top.Left = startOrg.X;
                    top.Top = startOrg.Y;
                    top.Height = 2;
                    top.Width = 100;
                    top.Show();
                    top.Refresh();
                }
            }
        }

        private void MouseDownMoveHandler()
        {
            lock (bmScreenLock)
            {
                if (bmScreen != null && left != null && top != null)
                {
                    int x = MousePosition.X - screenLeft;
                    int y = MousePosition.Y - screenTop;

                    start = new Point(startOrg.X < x ? startOrg.X : x, startOrg.Y < y ? startOrg.Y : y);
                    stop = new Point(startOrg.X > x ? startOrg.X : x, startOrg.Y > y ? startOrg.Y : y);

                    left.Left = start.X;
                    left.Top = start.Y;
                    left.Width = 2;
                    left.Height = stop.Y - start.Y;
                    left.Show();

                    top.Left = start.X;
                    top.Top = start.Y;
                    top.Height = 2;
                    top.Width = stop.X - start.X;
                    top.Show();

                    if (right == null)
                    {
                        right = new PictureBox();
                        right.BackColor = Color.Red;
                        Controls.Add(right);
                        right.BringToFront();
                        AssignEventHandlers(right);
                    }

                    right.Left = stop.X;
                    right.Top = start.Y;
                    right.Width = 2;
                    right.Height = stop.Y - start.Y;
                    right.Show();

                    if (bottom == null)
                    {
                        bottom = new PictureBox();
                        bottom.BackColor = Color.Red;
                        Controls.Add(bottom);
                        bottom.BringToFront();
                        AssignEventHandlers(bottom);
                    }

                    bottom.Left = start.X;
                    bottom.Top = stop.Y;
                    bottom.Height = 2;
                    bottom.Width = stop.X - start.X;
                    bottom.Show();

                    TopMost = true;
                    Show();
                }
            }
        }

        private void MouseUpHandler(bool canceled = false, bool rightMouseButton = false)
        {
            lock (bmScreenLock)
            {
                MouseDownMoveHandler();
                customScreenCaptureInProgress = 0;

                if (!canceled && bmScreen != null && left != null && top != null && right != null && bottom != null
                    && stop.X - start.X > 0 && stop.Y - start.Y > 0)
                {
                    start = PointInStandardDPI(start);
                    stop = PointInStandardDPI(stop);
                    Bitmap bm = new(stop.X - start.X, stop.Y - start.Y);
                    Graphics g = Graphics.FromImage(bm);

                    try
                    {
                        g.DrawImage(bmScreen, 0, 0, new Rectangle(start, bm.Size), GraphicsUnit.Pixel);
                        g.DrawRectangle(Pens.DarkOrange, 0, 0, stop.X - start.X - 1, stop.Y - start.Y - 1);

                        foreach (FocusArea f in focusZone)
                        {
                            RectangleF rec = RectangleFInStandardDPI(f.Rec);
                            rec.X -= start.X;
                            rec.Y -= start.Y;

                            g.DrawEllipse(new Pen(f.Color, 2), rec);
                        }

                        if (rightMouseButton)
                        {
                            try
                            {
                                string tempFile = Path.GetTempPath() + Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".png";
                                SendLog(tempFile);
                                bm.Save(tempFile, ImageFormat.Png);
                                lastClipboardEventTime = GetTick();
                                ClipboardMMHelper.SetText(tempFile);
                            }
                            catch (IOException ioException)
                            {
                                SendLog("IO!Exception!: " + ioException.Message);
                            }
                            catch (ArgumentNullException argException)
                            {
                                SendLog("ArgNull!Exception!: " + argException.Message);
                            }
                            catch (ExternalException internalException)
                            {
                                SendLog("Internal!Exception!: " + internalException.Message);
                            }
                        }
                        else
                        {
                            lastClipboardEventTime = GetTick() - 10000;
                            ClipboardMMHelper.SetImage(bm);
                        }
                    }
                    finally
                    {
                        g.Dispose();
                        bm.Dispose();
                        bmScreen.Dispose();
                        bmScreen = null;
                    }
                }

                ShowInTaskbar = false;
                Hide();
                Controls.Clear();
                Opacity = 0.11;
                left?.Dispose();
                top?.Dispose();
                right?.Dispose();
                bottom?.Dispose();
                picScr?.Dispose();
                left = top = right = bottom = picScr = null;

                SendLog("Screen capture ended.\r\n**************************************************");
            }
        }

        private void FormHelper_FormClosed(object sender, FormClosedEventArgs e)
        {
            ClipboardMMHelper.UnhookClipboard();
        }

        private void FormHelper_LocationChanged(object sender, EventArgs e)
        {
            lock (bmScreenLock)
            {
                if (picScr == null && !timerHelper.Enabled)
                {
                    Opacity = 0.11;
                    timerHelper.Start();
                }
            }
        }

        private void FormHelper_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                MouseUpHandler(true);
            }

            if (focus1.IsEmpty)
            {
                focus1 = stop;
            }
        }

        private void FormHelper_KeyUp(object sender, KeyEventArgs e)
        {
            focus2 = stop;

            float x = Math.Min(focus1.X, focus2.X);
            float y = Math.Min(focus1.Y, focus2.Y);
            float w = Math.Abs(focus1.X - focus2.X);
            float h = Math.Abs(focus1.Y - focus2.Y);
            x -= 0.25F * w;
            y -= 0.25F * h;
            w *= 1.5F;
            h *= 1.5F;

            focusZone.Add(new FocusArea(new RectangleF(x, y, w, h), e.KeyCode == Keys.B ? Color.Blue : e.KeyCode == Keys.G ? Color.Green : Color.Red));

            focus1 = focus2 = Point.Empty;
        }

        private void FormHelper_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
            }
        }

        private void FormHelper_MouseDown(object sender, MouseEventArgs e)
        {
            SendLog("Screen capture Mouse down.");
            MouseMoveHandler();
            customScreenCaptureInProgress = 2;
        }

        private void FormHelper_MouseMove(object sender, MouseEventArgs e)
        {
            if (customScreenCaptureInProgress == 1)
            {
                MouseMoveHandler();
            }
            else if (customScreenCaptureInProgress == 2)
            {
                MouseDownMoveHandler();
            }
        }

        private void FormHelper_MouseUp(object sender, MouseEventArgs e)
        {
            MouseUpHandler(false, e.Button == MouseButtons.Right);
        }

        private Point PointInStandardDPI(Point p)
        {
            // Since the process is DPI awareness, just log and return.
            // TODO: Test in Win8/7/XP.
            SendLog(string.Format(CultureInfo.CurrentCulture, "this.Width={0}, this.Height={1}, bmScreen.Width={2}, bmScreen.Height={3}, x={4}, y={5}", Width, Height, bmScreen.Width, bmScreen.Height, p.X, p.Y));
            return p;
        }

        private RectangleF RectangleFInStandardDPI(RectangleF r)
        {
            // Since the process is DPI awareness, just return.
            return r;
        }
    }
}
