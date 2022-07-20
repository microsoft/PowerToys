// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wox.Plugin;

namespace Wox.Plugin
{
    public class GenericSelectedItem
    {
        public string Name { get; set; }

        public int SelectedCount { get; set; }

        public string IconPath { get; set; }

        public int Score { get; set; }

        public string Title { get; set; }

        public string SubTitle { get; set; }

        public DateTime LastSelected { get; set; }

        public string PluginID { get; set; }

        public string Search { get; set; }

        public string ResultObject { get; set; }
    }
}
