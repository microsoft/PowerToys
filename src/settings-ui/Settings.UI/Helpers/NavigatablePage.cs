// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public abstract class NavigatablePage : Page
{
    private string _pendingElementKey;

    public NavigatablePage()
    {
        Loaded += OnPageLoaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _pendingElementKey = e.Parameter as string;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingElementKey))
        {
            var target = FindElementByAutomationId(this, _pendingElementKey);

            target?.StartBringIntoView(new BringIntoViewOptions
            {
                VerticalOffset = -20,
                AnimationDesired = true,
            });

            await OnTargetElementNavigatedAsync(target, _pendingElementKey);

            _pendingElementKey = null;
        }
    }

    protected virtual async Task OnTargetElementNavigatedAsync(FrameworkElement target, string elementKey)
    {
        if (target is Control ctrl)
        {
            var oldBrush = ctrl.BorderBrush;
            var oldThickness = ctrl.BorderThickness;

            ctrl.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            ctrl.BorderThickness = new Microsoft.UI.Xaml.Thickness(2);

            await Task.Delay(1000);

            ctrl.BorderBrush = oldBrush;
            ctrl.BorderThickness = oldThickness;
        }
        else
        {
        }
    }

    protected static FrameworkElement FindElementByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement fe)
        {
            var id = AutomationProperties.GetAutomationId(fe);
            if (!string.IsNullOrEmpty(id) && id == automationId)
            {
                return fe;
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var result = FindElementByAutomationId(child, automationId);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    protected static FrameworkElement FindElementByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe)
        {
            if (!string.IsNullOrEmpty(fe.Name) && fe.Name == name)
            {
                return fe;
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var result = FindElementByName(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
