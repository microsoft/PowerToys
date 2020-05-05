using Microsoft.PowerLauncher.Telemetry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace PowerLauncher.UI
{
    public sealed partial class ResultList : UserControl
    {
        private ResultActionEvent.TriggerType triggerType = ResultActionEvent.TriggerType.Click;
        public ResultList()
        {
            InitializeComponent();
        }


        private void ContextButton_OnAcceleratorInvoked(Windows.UI.Xaml.Input.KeyboardAccelerator sender, Windows.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            this.triggerType = ResultActionEvent.TriggerType.KeyboardShortcut;
        }

        private void ContextButton_OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var button = sender as Windows.UI.Xaml.Controls.Button;

            if (button != null)
            {
                //We currently can't take a reference on the wox project from a UWP project.  The dynamic method invoke should be replace
                //by an call to the view model once we refactor the project.
                var dataContext = ((dynamic)button.DataContext);
                if(dataContext?.GetType().GetMethod("SendTelemetryEvent") != null)
                {
                    dataContext.SendTelemetryEvent(triggerType);
                }
            }

            //Restore the trigger type back to click
            triggerType = ResultActionEvent.TriggerType.Click;
        }
    }
}
