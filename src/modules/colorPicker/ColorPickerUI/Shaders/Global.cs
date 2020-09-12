// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ColorPicker.Shaders
{
    internal static class Global
    {
        /// <summary>
        /// Helper method for generating a "pack://" URI for a given relative file based on the
        /// assembly that this class is in.
        /// </summary>
        public static Uri MakePackUri(string relativeFile)
        {
            string uriString = "pack://application:,,,/" + AssemblyShortName + ";component/Shaders/" + relativeFile;
            return new Uri(uriString);
        }

        private static string AssemblyShortName
        {
            get
            {
                if (_assemblyShortName == null)
                {
                    var assembly = typeof(Global).Assembly;

                    // Pull out the short name.
                    _assemblyShortName = assembly.ToString().Split(',')[0];
                }

                return _assemblyShortName;
            }
        }

        private static string _assemblyShortName;
    }
}
