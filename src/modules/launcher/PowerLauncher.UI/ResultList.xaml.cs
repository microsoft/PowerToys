using Microsoft.PowerLauncher.Telemetry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PowerLauncher.UI
{
    public sealed partial class ResultList : UserControl
    {
        private Brush _borderBrush;

        private LauncherResultActionEvent.TriggerType triggerType = LauncherResultActionEvent.TriggerType.Click;
        
        public ResultList()
        {
            InitializeComponent();
        }


        private void ContextButton_OnAcceleratorInvoked(Windows.UI.Xaml.Input.KeyboardAccelerator sender, Windows.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            this.triggerType = LauncherResultActionEvent.TriggerType.KeyboardShortcut;
        }

        private void ContextButton_OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as Windows.UI.Xaml.Controls.Button;

            if (button != null)
            {
                //We currently can't take a reference on the wox project from a UWP project.  The dynamic method invocation should be replaced
                //by a call to the view model once we refactor the project.
                var dataContext = ((dynamic)button.DataContext);
                if(dataContext?.GetType().GetMethod("SendTelemetryEvent") != null)
                {
                    dataContext.SendTelemetryEvent(triggerType);
                }
            }

            //Restore the trigger type back to click
            triggerType = LauncherResultActionEvent.TriggerType.Click;
        }

        public Brush SolidBorderBrush
        {
            get { return _borderBrush; }
            set { Set(ref _borderBrush, value); }
        }

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void UserControl_ActualThemeChanged(FrameworkElement sender, object args)
        {
            SolidBorderBrush = Application.Current.Resources["SystemChromeLow"] as SolidColorBrush;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SolidBorderBrush = Application.Current.Resources["SystemChromeLow"] as SolidColorBrush;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
