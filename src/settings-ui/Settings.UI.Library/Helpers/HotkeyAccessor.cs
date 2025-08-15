// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public class HotkeyAccessor
    {
        public Func<HotkeySettings> Getter { get; }

        public Action<HotkeySettings> Setter { get; }

        public HotkeyAccessor(Func<HotkeySettings> getter, Action<HotkeySettings> setter, string localizationHeaderKey = "")
        {
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Setter = setter ?? throw new ArgumentNullException(nameof(setter));
            LocalizationHeaderKey = localizationHeaderKey;
        }

        public HotkeySettings Value
        {
            get => Getter();
            set => Setter(value);
        }

        public string LocalizationHeaderKey { get; set; }
    }
}
