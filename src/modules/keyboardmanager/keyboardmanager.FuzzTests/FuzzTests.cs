// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManager.FuzzTests
{
    public class FuzzTests
    {
        // Case1: Fuzzing method for ParseSingleKeyRemap
        private static IntPtr _configHandle;

        public static void FuzzAddSingleKeyToTextRemap(ReadOnlySpan<byte> input)
        {
            string remap = Encoding.UTF8.GetString(input);
            _configHandle = KeyboardManagerInterop.CreateMappingConfiguration();

            int originalKey = 28;

            _ = KeyboardManagerInterop.AddSingleKeyToTextRemap(_configHandle, originalKey, remap);
        }
    }
}
