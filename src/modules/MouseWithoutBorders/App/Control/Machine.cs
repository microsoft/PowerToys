// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Forms;

// <summary>
//     User control, used in the Matrix form.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using MouseWithoutBorders.Class;
using MouseWithoutBorders.Properties;

namespace MouseWithoutBorders
{
    internal partial class Machine : UserControl
    {
        // private int ip;
        // private Point mouseDownPos;
        private SocketStatus statusClient;

        private SocketStatus statusServer;
        private bool localhost;

        internal Machine()
        {
            InitializeComponent();
            Visible = false;
            MachineEnabled = false;
        }

        internal string MachineName
        {
            get => textBoxName.Text;
            set => textBoxName.Text = value;
        }

        internal bool MachineEnabled
        {
            get => checkBoxEnabled.Checked;
            set
            {
                checkBoxEnabled.Checked = value;
                Editable = value;
                pictureBoxLogo.Image = value ? Images.MachineEnabled : (System.Drawing.Image)Images.MachineDisabled;
                OnEnabledChanged(EventArgs.Empty); // Borrow this event since we do not use it for any other purpose:) (we can create one but l...:))
            }
        }

        internal bool Editable
        {
            set => textBoxName.Enabled = value;

            // get { return textBoxName.Enabled;  }
        }

        internal bool CheckAble
        {
            set
            {
                if (!value)
                {
                    checkBoxEnabled.Checked = true;
                    Editable = false;
                }

                checkBoxEnabled.Enabled = value;
            }
        }

        internal bool LocalHost
        {
            // get { return localhost; }
            set
            {
                localhost = value;
                if (localhost)
                {
                    labelStatusClient.Text = "local machine";
                    labelStatusServer.Text = "...";
                    CheckAble = false;
                }
                else
                {
                    labelStatusClient.Text = "...";
                    labelStatusServer.Text = "...";
                    CheckAble = true;
                }
            }
        }

        private void PictureBoxLogo_MouseDown(object sender, MouseEventArgs e)
        {
            // mouseDownPos = e.Location;
            OnMouseDown(e);
        }

        /*
        internal Point MouseDownPos
        {
            get { return mouseDownPos; }
        }
        */

        private void CheckBoxEnabled_CheckedChanged(object sender, EventArgs e)
        {
            MachineEnabled = checkBoxEnabled.Checked;
        }

        private static string StatusString(SocketStatus status)
        {
            string rv = string.Empty;

            switch (status)
            {
                case SocketStatus.Resolving:
                    rv = "Resolving";
                    break;

                case SocketStatus.Connected:
                    rv = "Connected";
                    break;

                case SocketStatus.Connecting:
                    rv = "Connecting";
                    break;

                case SocketStatus.Error:
                    rv = "Error";
                    break;

                case SocketStatus.ForceClosed:
                    rv = "Closed";
                    break;

                case SocketStatus.Handshaking:
                    rv = "Handshaking";
                    break;

                case SocketStatus.SendError:
                    rv = "Send error";
                    break;

                case SocketStatus.InvalidKey:
                    rv = "KeysNotMatched";
                    break;

                case SocketStatus.Timeout:
                    rv = "Timed out";
                    break;

                case SocketStatus.NA:
                    rv = "...";
                    break;

                default:
                    break;
            }

            return rv;
        }

        internal SocketStatus StatusClient
        {
            get => statusClient;

            set
            {
                statusClient = value;
                if (statusClient is SocketStatus.Connected or
                    SocketStatus.Handshaking)
                {
                    Editable = false;
                }

                labelStatusClient.Text = StatusString(statusClient) + " -->";
            }
        }

        internal SocketStatus StatusServer
        {
            get => statusServer;

            set
            {
                statusServer = value;
                if (statusServer is SocketStatus.Connected or
                    SocketStatus.Handshaking)
                {
                    Editable = false;
                }

                labelStatusServer.Text = StatusString(statusServer) + " <--";
            }
        }

        private void PictureBoxLogo_Paint(object sender, PaintEventArgs e)
        {
            // e.Graphics.DrawString("(Draggable)", this.Font, Brushes.WhiteSmoke, 20, 15);
        }
    }
}
