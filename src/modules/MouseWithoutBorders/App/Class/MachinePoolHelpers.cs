// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.Class
{
    internal static class MachinePoolHelpers
    {
        internal static readonly char[] Comma = new char[] { ',' };
        internal static readonly char[] Colon = new char[] { ':' };

        internal static MachineInf[] LoadMachineInfoFromMachinePoolStringSetting(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(s);
            }

            string[] st = s.Split(Comma);

            if (st.Length < MachineStuff.MAX_MACHINE)
            {
                throw new ArgumentException("Not enough elements in encoded MachinePool string");
            }

            MachineInf[] rv = new MachineInf[MachineStuff.MAX_MACHINE];
            for (int i = 0; i < MachineStuff.MAX_MACHINE; i++)
            {
                string[] mc = st[i].Split(Colon);
                if (mc.Length == 2)
                {
                    rv[i].Name = mc[0];
                    rv[i].Id = uint.TryParse(mc[1], out uint ip) ? (ID)ip : ID.NONE;
                    rv[i].Time = rv[i].Id == ID.NONE ? Common.GetTick() - MachineStuff.HEARTBEAT_TIMEOUT : Common.GetTick();
                }
            }

            return rv;
        }
    }
}
