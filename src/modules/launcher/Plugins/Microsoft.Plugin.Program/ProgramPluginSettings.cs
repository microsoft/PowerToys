// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Plugin.Program
{
    public class ProgramPluginSettings
    {
        public DateTime LastIndexTime { get; set; }

        public List<ProgramSource> ProgramSources { get;} = new List<ProgramSource>();

        public List<DisabledProgramSource> DisabledProgramSources { get;} = new List<DisabledProgramSource>();

        public List<string> ProgramSuffixes { get; } = new List<string>(){ "bat", "appref-ms", "exe", "lnk", "url" };

        public bool EnableStartMenuSource { get; set; } = true;

        public bool EnableDesktopSource { get; set; } = true;

        public bool EnableRegistrySource { get; set; } = true;

        public bool EnablePathEnvironmentVariableSource { get; set; } = true;

        public double MinScoreThreshold { get; set; } = 0.75;

        internal const char SuffixSeparator = ';';

    }

    /// <summary>
    /// Contains user added folder location contents as well as all user disabled applications
    /// </summary>
    /// <remarks>
    /// <para>Win32 class applications set UniqueIdentifier using their full file path</para>
    /// <para>UWP class applications set UniqueIdentifier using their Application User Model ID</para>
    /// <para>Custom user added program sources set UniqueIdentifier using their location</para>
    /// </remarks>
    public class ProgramSource
    {
        private string name;

        public string Location { get; set; }

        public string Name { get => name ?? new DirectoryInfo(Location).Name; set => name = value; }

        public bool Enabled { get; set; } = true;

        public string UniqueIdentifier { get; set; }
    }

    public class DisabledProgramSource : ProgramSource { }
}
