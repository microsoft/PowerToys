// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace Awake.Core
{
    public class TrayMessageFilter : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeConstants.WM_COMMAND:
                    switch (m.WParam.ToInt64() & 0xFFFF)
                    {
                        case NativeConstants.WM_USER + 1:
                            // TODO
                            break;
                        case NativeConstants.WM_USER + 2:
                            // TODO
                            break;
                    }

                    break;
            }

            return false;
        }
    }
}
