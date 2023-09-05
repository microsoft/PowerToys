// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// This line is importing the System.ComponentModel namespace, which contains interfaces 
// that are used to implement the run-time and design-time behavior of components and controls.
using System.ComponentModel;

// Declaring a namespace named "ColorPicker.Settings". Namespaces are used to organize 
// your code and can contain multiple classes, structs, etc.
namespace ColorPicker.Settings
{
    // Declaring a public class named "SettingItem" with a generic type parameter "T". 
    // This class also implements the INotifyPropertyChanged interface which is used 
    // to notify clients, typically binding clients, that a property value has changed.
    public sealed class SettingItem<T> : INotifyPropertyChanged
    {
        // Declaring a private field of type "T" named "_value" to hold the value of the setting item.
        private T _value;

        // This is a constructor that takes a parameter "startValue" of type "T" 
        // and initializes the "_value" field with the "startValue".
        public SettingItem(T startValue)
        {
            _value = startValue;
        }

        // This is a property of type "T" named "Value". It has a getter that returns 
        // the value of the "_value" field and a setter that sets the value of the 
        // "_value" field and calls the "OnValueChanged" method.
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

        // Declaring an event named "PropertyChanged" of type "PropertyChangedEventHandler". 
        // This event is raised whenever the value of the "Value" property changes.
        public event PropertyChangedEventHandler PropertyChanged;

        // Declaring a private method named "OnValueChanged" that is called whenever 
        // the value of the "Value" property changes. It raises the "PropertyChanged" 
        // event with the name of the "Value" property as the argument.
        private void OnValueChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }
}
