// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    // A program that is making sound right now, as the Sound Manager module interface sees it.
    public sealed class SoundManagerProgram : Observable
    {
        private string _program = string.Empty;
        private string _name = string.Empty;
        private string _processPath = string.Empty;

        // The file name of the executable file, which is what tells one program from another.
        public string Program
        {
            get => _program;
            set => Set(ref _program, value);
        }

        // The name of the program, which is the one that is shown to the user.
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        // The place of the executable file, which is where the icon of the program is kept.
        public string ProcessPath
        {
            get => _processPath;
            set => Set(ref _processPath, value);
        }
    }
}
