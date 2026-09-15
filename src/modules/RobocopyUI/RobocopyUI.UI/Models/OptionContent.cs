// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RobocopyUI.Models
{
    public class OptionContent : INotifyPropertyChanged
    {
        private bool enabled;

        private int intValue;

        private string optionName = string.Empty;

        private string optionDescription = string.Empty;

        private bool isNumberOption;

        private bool isMultiSelectOption;

        private List<OptionContent> multiSelectOptions = new();

        private bool isRunHoursOption;

        private bool isStorageOption;

        private bool isTextOption;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool Enabled
        {
            get => enabled;
            set => SetProperty(ref enabled, value);
        }

        public int IntValue
        {
            get => intValue;
            set => SetProperty(ref intValue, value);
        }

        public string OptionName
        {
            get => optionName;
            set => SetProperty(ref optionName, value);
        }

        public string OptionDescription
        {
            get => optionDescription;
            set => SetProperty(ref optionDescription, value);
        }

        public bool IsNumberOption
        {
            get => isNumberOption;
            set => SetProperty(ref isNumberOption, value);
        }

        public bool IsMultiSelectOption
        {
            get => isMultiSelectOption;
            set => SetProperty(ref isMultiSelectOption, value);
        }

        public List<OptionContent> MultiSelectOptions
        {
            get => multiSelectOptions;
            set => SetProperty(ref multiSelectOptions, value);
        }

        public bool IsRunHoursOption
        {
            get => isRunHoursOption;
            set => SetProperty(ref isRunHoursOption, value);
        }

        public bool IsStorageOption
        {
            get => isStorageOption;
            set => SetProperty(ref isStorageOption, value);
        }

        public bool IsTextOption
        {
            get => isTextOption;
            set => SetProperty(ref isTextOption, value);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
