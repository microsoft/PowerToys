// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace PowerOCR.Settings;

public sealed class SettingItem<T> : INotifyPropertyChanged
{
    private T _value;

    public SettingItem(T startValue)
    {
        _value = startValue;
    }

    public T Value
    {
        get
        {
            return _value;
        }

        set
        {
            _value = value;
            OnValueChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnValueChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}
