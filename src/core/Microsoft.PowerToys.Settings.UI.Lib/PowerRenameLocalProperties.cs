// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameLocalProperties
    {
        public PowerRenameLocalProperties()
        {
            PersistState = false;
            MRUEnabled = false;
            MaxMRUSize = 0;
            ShowIcon = false;
            ExtendedContextMenuOnly = false;
        }

        private int _maxSize;

        public bool PersistState { get; set; }

        public bool MRUEnabled { get; set; }

        public int MaxMRUSize
        {
            get
            {
                return _maxSize;
            }

            set
            {
                if (value < 0)
                {
                    _maxSize = 0;
                }
                else
                {
                    _maxSize = value;
                }
            }
        }

        public bool ShowIcon { get; set; }

        public bool ExtendedContextMenuOnly { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
