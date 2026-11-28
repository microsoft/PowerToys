// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander.Editor.Helpers
{
    /// <summary>What Windows has set to open a type of file.</summary>
    internal static partial class TextExpanderFileAssociation
    {
        // ASSOCF_INIT_IGNOREUNKNOWN: a type with no app is an error, rather than the "Unknown" type, which opens Open With.
        private const uint IgnoreUnknown = 0x400;

        private const int AssocStrCommand = 1;

        private const int AssocStrDelegateExecute = 18;

        /// <summary>
        /// Gets a value indicating whether an app is set to open files with <paramref name="extension"/>, such as ".json":
        /// a command, or a packaged app's handler. Without one, Windows asks which app to use instead of opening the file.
        /// </summary>
        public static bool HasDefaultApp(string extension)
            => Has(extension, AssocStrCommand) || Has(extension, AssocStrDelegateExecute);

        private static bool Has(string extension, int what)
        {
            // With no buffer, only the length is asked for, and S_FALSE says there's a value.
            uint length = 0;
            int result = AssocQueryString(IgnoreUnknown, what, extension, null, IntPtr.Zero, ref length);
            return result is 0 or 1 && length > 1;
        }

        [LibraryImport("shlwapi.dll", EntryPoint = "AssocQueryStringW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int AssocQueryString(uint flags, int what, string association, string extra, IntPtr output, ref uint length);
    }
}
