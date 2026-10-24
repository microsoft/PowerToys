// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class PowerAccentShortcutControl : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(PowerAccentShortcutControl), new PropertyMetadata(default(string)));

        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Dependency property wrappers must remain settable, and changing the public property type would break existing consumers.")]
        public List<object> Keys
        {
            get { return (List<object>)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register("Keys", typeof(List<object>), typeof(PowerAccentShortcutControl), new PropertyMetadata(default(List<object>)));

        public PowerAccentShortcutControl()
        {
            this.InitializeComponent();
        }
    }
}
