// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewProperties
    {
        public BoolProperty IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL { get; set; }

        public BoolProperty PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID { get; set; }

        public PowerPreviewProperties()
        {
            this.IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL = new BoolProperty();
            this.PREVPANE_MD_BOOL_TOGGLE_CONTROLL_ID = new BoolProperty();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
