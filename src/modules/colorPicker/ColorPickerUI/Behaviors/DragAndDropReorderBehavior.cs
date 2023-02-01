// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorPicker.Models;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class DragAndDropReorderBehavior : Behavior<ItemsControl>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            var style = new Style(typeof(ContentPresenter));
            style.Setters.Add(
                new EventSetter(
                    FrameworkElement.PreviewMouseMoveEvent,
                    new MouseEventHandler(ItemPreviewMouseMove)));
            style.Setters.Add(
                    new EventSetter(
                        FrameworkElement.DropEvent,
                        new DragEventHandler(ItemDrop)));
            AssociatedObject.ItemContainerStyle = style;
        }

        private void ItemPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Task.Run(
                    new Action(() =>
                    {
                        Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                if (e.LeftButton == MouseButtonState.Pressed)
                                {
                                    var data = new DataObject();
                                    data.SetData("Source", (sender as FrameworkElement).DataContext);
                                    DragDrop.DoDragDrop(sender as DependencyObject, data, DragDropEffects.Move);
                                    e.Handled = true;
                                }
                            }),
                            null);
                    }),
                    CancellationToken.None);
            }
        }

        private void ItemDrop(object sender, DragEventArgs e)
        {
            var source = e.Data.GetData("Source") as ColorFormatModel;
            if (source != null)
            {
                int newIndex = AssociatedObject.Items.IndexOf((sender as FrameworkElement).DataContext);
                var list = AssociatedObject.ItemsSource as ObservableCollection<ColorFormatModel>;
                list.RemoveAt(list.IndexOf(source));
                list.Insert(newIndex, source);
            }
        }
    }
}
