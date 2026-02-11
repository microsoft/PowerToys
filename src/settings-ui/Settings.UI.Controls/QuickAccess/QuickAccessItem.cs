// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed class QuickAccessItem : Observable
    {
        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _description = string.Empty;

        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private string _icon = string.Empty;

        public string Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        private ICommand? _command;

        public ICommand? Command
        {
            get => _command;
            set => Set(ref _command, value);
        }

        private object? _commandParameter;

        public object? CommandParameter
        {
            get => _commandParameter;
            set => Set(ref _commandParameter, value);
        }

        private bool _visible = true;

        public bool Visible
        {
            get => _visible;
            set => Set(ref _visible, value);
        }

        private object? _tag;

        public object? Tag
        {
            get => _tag;
            set => Set(ref _tag, value);
        }
    }
}
