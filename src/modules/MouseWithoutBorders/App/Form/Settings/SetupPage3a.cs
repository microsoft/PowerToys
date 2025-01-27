// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

using MouseWithoutBorders.Class;
using MouseWithoutBorders.Core;
using MouseWithoutBorders.Properties;

namespace MouseWithoutBorders
{
    public partial class SetupPage3a : SettingsFormPage
    {
        private readonly Image[] _frames = { Images.copy_paste_example, Images.drag_example, Images.keyboard_example };
        private readonly string[] _messages =
        {
                                                  "Copy && paste across screens",
                                                  "Drag files across screens",
                                                  "Share keyboard across screens",
        };

        private readonly int[] _timing = { 1000, 1000, 2000 };
#pragma warning disable CA2213 // Disposing is done by ComponentResourceManager
        private readonly System.Timers.Timer _animationTimer;
#pragma warning restore CA2213
        private bool connected;
        private bool done;
        private bool invalidKey;

        private int _animationFrame;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ReturnToSettings { get; set; }

        public SetupPage3a()
        {
            InitializeComponent();
            _animationTimer = new System.Timers.Timer { Interval = 1000 };
        }

        private void ShowStatus(string status)
        {
            labelStatus.Text = status;
            Logger.Log(status);
        }

        public override void OnPageClosing()
        {
            _animationTimer.Stop();
            base.OnPageClosing();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _animationTimer.Elapsed += AnimationTimerTick;

            Common.GetMachineName();
            invalidKey = false;

            UpdateAnimation();
            _animationTimer.Start();

            SocketStuff.InvalidKeyFound = false;
            SocketStuff.InvalidKeyFoundOnClientSocket = false;

            ShowStatus($"Connecting...");

            MachineStuff.SwitchToMultipleMode(false, false);
            Common.ReopenSockets(true);

            int timeOut = 0;
            TcpSk connectedClientSocket;

            // Client sockets run in different threads.
            while (timeOut < 10)
            {
                Common.MMSleep(1);

                if ((connectedClientSocket = Common.GetConnectedClientSocket()) != null)
                {
                    ShowStatus($"Connected from local IP Address: {connectedClientSocket.Address}.");
                    Common.UpdateMachineTimeAndID();

                    Common.MMSleep(1);
                    connected = true;
                    return;
                }
                else if (SocketStuff.InvalidKeyFoundOnClientSocket)
                {
                    invalidKey = true;
                    ShowStatus("Status: InvalidKey.");
                    Common.MMSleep(3);
                    break;
                }

                timeOut++;
            }

            done = true;
        }

        private void AnimationTimerTick(object sender, EventArgs e)
        {
            _ = Invoke(new MethodInvoker(UpdateAnimation));
        }

        private void UpdateAnimation()
        {
            if ((ModifierKeys & Keys.Control) != 0)
            {
                _animationTimer.Stop();
                SendNextPage(new SetupPage2ab());
                return;
            }

            ExamplePicture.Image = _frames[_animationFrame];
            MessageLabel.Text = _messages[_animationFrame];
            _animationTimer.Interval = _timing[_animationFrame];
            _animationFrame = (_animationFrame + 1) % _frames.Length;

            if (connected)
            {
                _animationTimer.Stop();
                SendNextPage(new SetupPage4());
            }
            else if (done)
            {
                _animationTimer.Stop();
                SendNextPage(new SetupPage2ab());

                if (invalidKey)
                {
                    Common.ShowToolTip(
                        "Security Codes not matched.\r\nVerify that you entered the same code in all machines.\r\nAnd make sure you run the same version of "
                        + Application.ProductName + " in all machines.\r\nThis version: " + FrmAbout.AssemblyVersion,
                        20000);
                }
                else
                {
                    string helpText = "Connection error!";
                    helpText += "\r\nPlease check if your machines are connected to the same network, note that " + Application.ProductName + " works better with wired connection.";
                    helpText += "\r\nAnd double check if " + Application.ProductName + " is allowed through firewall in all machines.";
                    Common.ShowToolTip(helpText, 30000);
                }
            }
        }
    }
}
