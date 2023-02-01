using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using WinRT.Interop;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Graphics;

namespace RegistryPreview
{
    public sealed partial class MainWindow : Window
    {
        /// <summary>
        /// Event handler to grab the main window's size and position before it closes
        /// </summary>
        private void m_appWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            m_applicationDataContainer.Values["m_appWindow.Position.X"] = (int)m_appWindow.Position.X;
            m_applicationDataContainer.Values["m_appWindow.Position.Y"] = (int)m_appWindow.Position.Y;
            m_applicationDataContainer.Values["m_appWindow.Size.Width"] = (int)m_appWindow.Size.Width;
            m_applicationDataContainer.Values["m_appWindow.Size.Height"] = (int)m_appWindow.Size.Height;
        }


        /// <summary>
        /// Event that is will prevent the app from closing if the "save file" flag is active
        /// </summary>   
        public void Window_Closed(object sender, WindowEventArgs args)
        {
            // Only block closing if the REG file has been edited but not yet saved
            if (saveButton.IsEnabled)
            {
                // if true, the app will not close
                args.Handled = true;

                // ask the user if they want to save, discard or cancel the close; strings must be loaded here and passed to avoid timing issues
                HandleDirtyClosing(
                    m_resourceLoader.GetString("YesNoCancelDialogTitle"),
                    m_resourceLoader.GetString("YesNoCancelDialogContent"),
                    m_resourceLoader.GetString("YesNoCancelDialogPrimaryButtonText"),
                    m_resourceLoader.GetString("YesNoCancelDialogSecondaryButtonText"),
                    m_resourceLoader.GetString("YesNoCancelDialogCloseButtonText")
                    );

            }

            // Save app settings
            m_applicationDataContainer.Values["checkBoxTextBox.Checked"] = checkBoxTextBox.IsChecked;
        }

        /// <summary>
        /// Event that gets fired after the visual tree has been fully loaded; the app opens the reg file from here so it can show a message box successfully
        /// </summary>   
        private void gridPreview_Loaded(object sender, RoutedEventArgs e)
        {
            // static flag to track whether the Visual Tree is ready - if the main Grid has been loaded, the tree is ready.
            m_visualTreeReady = true;

            // Load and restore app settings
            if (m_applicationDataContainer.Values["checkBoxTextBox.Checked"] != null)
            {
                checkBoxTextBox.IsChecked = (bool)m_applicationDataContainer.Values["checkBoxTextBox.Checked"];
            }

            // Check to see if the REG file was opened and parsed successfully
            if (OpenRegistryFile(App.s_Filename) == false)
            {
                // Allow Refresh and Edit to be enabled because a broken Reg file might be fixable
                UpdateToolBarAndUI(false, true, true);
                UpdateWindowTitle(m_resourceLoader.GetString("InvalidRegistryFileTitle"));
                return;
            }

            // Some days you have to select the node yourself - unclear as to why, but why lose this neat code?
            //if (treeView.RootNodes.Count > 0)
            //{
            //    TreeViewNode node = treeView.RootNodes[0];
            //    TreeViewItem item = (TreeViewItem)treeView.ContainerFromNode(node);
            //    if (item != null)
            //    {
            //        item.TabIndex = 0;
            //        item.IsTabStop = true;
            //        item.Focus(FocusState.Programmatic);
            //    }
            //}

            // resize the window
            if (m_applicationDataContainer.Values["m_appWindow.Size.Width"] != null)
            {
                SizeInt32 size;
                size.Width = (int)m_applicationDataContainer.Values["m_appWindow.Size.Width"];
                size.Height = (int)m_applicationDataContainer.Values["m_appWindow.Size.Height"];
                m_appWindow.Resize(size);
            }
            
            // reposition the window
            if (m_applicationDataContainer.Values["m_appWindow.Position.X"] != null)
            {
                PointInt32 point;
                point.X = (int)m_applicationDataContainer.Values["m_appWindow.Position.X"];
                point.Y = (int)m_applicationDataContainer.Values["m_appWindow.Position.Y"];
                m_appWindow.Move(point);
            }

            textBox.Focus(FocusState.Programmatic);

            // hookup the event handler here to avoid accidental ability to Save
            textBox.TextChanged += textBox_TextChanged;
        }

        /// <summary>
        /// Uses a picker to select a new file to open
        /// </summary>   
        private async void openButton_Click(object sender, RoutedEventArgs e)
        {
            // Check to see if the current file has been saved
            if (saveButton.IsEnabled)
            {
                ContentDialog contentDialog = new ContentDialog()
                {
                    Title = m_resourceLoader.GetString("YesNoCancelDialogTitle"),
                    Content = m_resourceLoader.GetString("YesNoCancelDialogContent"),
                    PrimaryButtonText = m_resourceLoader.GetString("YesNoCancelDialogPrimaryButtonText"),
                    SecondaryButtonText = m_resourceLoader.GetString("YesNoCancelDialogSecondaryButtonText"),
                    CloseButtonText = m_resourceLoader.GetString("YesNoCancelDialogCloseButtonText"),
                    DefaultButton = ContentDialogButton.Primary
                };

                // Use this code to associate the dialog to the appropriate AppWindow by setting
                // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    contentDialog.XamlRoot = this.Content.XamlRoot;
                }

                ContentDialogResult contentDialogResult = await contentDialog.ShowAsync();
                switch (contentDialogResult)
                {
                    case ContentDialogResult.Primary:
                        // Save, then continue the file open
                        SaveFile();
                        break;
                    case ContentDialogResult.Secondary:
                        // Don't save and continue the file open!
                        saveButton.IsEnabled = false;
                        break;
                    default:
                        // Don't open the new file!
                        return;
                }
            }

            // Pull in a new REG file
            FileOpenPicker fileOpenPicker = new FileOpenPicker();
            fileOpenPicker.ViewMode = PickerViewMode.List;
            fileOpenPicker.CommitButtonText = m_resourceLoader.GetString("OpenButtonText");
            fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            fileOpenPicker.FileTypeFilter.Add(".reg");

            // Get the HWND so we an open the modal
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(fileOpenPicker, hWnd);

            StorageFile storageFile = await fileOpenPicker.PickSingleFileAsync();

            if (storageFile != null)
            {
                // mute the TextChanged handler to make for clean UI
                textBox.TextChanged -= textBox_TextChanged;
 
                App.s_Filename = storageFile.Path;
                UpdateToolBarAndUI(OpenRegistryFile(App.s_Filename));

                // disable the Save button as it's a new file
                saveButton.IsEnabled = false;

                // Restore the event handler as we're loaded
                textBox.TextChanged += textBox_TextChanged;
            }
        }

        /// <summary>
        /// Saves the currently opened file in place
        /// </summary>   
        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFile();
        }

        /// <summary>
        /// Uses a picker to save out a copy of the current reg file
        /// </summary>   
        private async void saveAsButton_Click(object sender, RoutedEventArgs e)
        {
            // Save out a new REG file and then open it
            FileSavePicker fileSavePicker = new FileSavePicker();
            fileSavePicker.CommitButtonText = m_resourceLoader.GetString("SaveButtonText");
            fileSavePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            fileSavePicker.FileTypeChoices.Add("Registry file", new List<String>() { ".reg" });
            fileSavePicker.SuggestedFileName = m_resourceLoader.GetString("SuggestFileName");

            // Get the HWND so we an save the modal
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(fileSavePicker, hWnd);

            StorageFile storageFile = await fileSavePicker.PickSaveFileAsync();

            if (storageFile != null)
            {
                App.s_Filename = storageFile.Path;
                SaveFile();
                UpdateToolBarAndUI(OpenRegistryFile(App.s_Filename));
            }
        }

        /// <summary>
        /// Reloads the current REG file from storage
        /// </summary>   
        private void refreshButton_Click(object sender, RoutedEventArgs e)
        {
            // mute the TextChanged handler to make for clean UI
            textBox.TextChanged -= textBox_TextChanged;

            // reload the current Registry file and update the toolbar accordingly.
            UpdateToolBarAndUI(OpenRegistryFile(App.s_Filename), true, true);

            saveButton.IsEnabled = false;

            // restore the TextChanged handler
            textBox.TextChanged += textBox_TextChanged;
        }

        /// <summary>
        /// Opens the Registry Editor; UAC is handled by the request to open
        /// </summary>   
        private void registryButton_Click(object sender, RoutedEventArgs e)
        {
            // pass in an empty string as we have no file to open
            OpenRegistryEditor("");
        }

        /// <summary>
        /// Merges the currently saved file into the Registry Editor; UAC is handled by the request to open
        /// </summary>   
        private async void writeButton_Click(object sender, RoutedEventArgs e)
        {
            // Check to see if the current file has been saved
            if (saveButton.IsEnabled)
            {
                ContentDialog contentDialog = new ContentDialog()
                {
                    Title = m_resourceLoader.GetString("YesNoCancelDialogTitle"),
                    Content = m_resourceLoader.GetString("YesNoCancelDialogContent"),
                    PrimaryButtonText = m_resourceLoader.GetString("YesNoCancelDialogPrimaryButtonText"),
                    SecondaryButtonText = m_resourceLoader.GetString("YesNoCancelDialogSecondaryButtonText"),
                    CloseButtonText = m_resourceLoader.GetString("YesNoCancelDialogCloseButtonText"),
                    DefaultButton = ContentDialogButton.Primary
                };

                // Use this code to associate the dialog to the appropriate AppWindow by setting
                // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    contentDialog.XamlRoot = this.Content.XamlRoot;
                }

                ContentDialogResult contentDialogResult = await contentDialog.ShowAsync();
                switch (contentDialogResult)
                {
                    case ContentDialogResult.Primary:
                        // Save, then continue the file open
                        SaveFile();
                        break;
                    case ContentDialogResult.Secondary:
                        // Don't save and continue the file open!
                        saveButton.IsEnabled = false;
                        break;
                    default:
                        // Don't open the new file!
                        return;
                }
            }

            // pass in the filename so we can edit the current file
            OpenRegistryEditor(App.s_Filename);
        }

        /// <summary>
        /// Opens the currently saved file in the PC's default REG file editor (often Notepad)
        /// </summary>   
        private void editButton_Click(object sender, RoutedEventArgs e)
        {
            // use the REG file's filename and verb so we can respect the selected editor
            Process process = new Process();
            process.StartInfo.FileName= String.Format("\"{0}\"", App.s_Filename);
            process.StartInfo.Verb = "Edit";
            process.StartInfo.UseShellExecute = true;

            try
            {
                process.Start();
            }
            catch
            {
                ShowMessageBox(
                    m_resourceLoader.GetString("ErrorDialogTitle"),
                    m_resourceLoader.GetString("FileEditorError"),
                    m_resourceLoader.GetString("OkButtonText")
                );
            }
        }

        /// <summary>
        /// Trigger that fires when a node in treeView is clicked and which populates dataGrid
        /// Can also be fired from elsewhere in the code
        /// </summary>   
        private void treeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            TreeViewItemInvokedEventArgs localArgs = args as TreeViewItemInvokedEventArgs;
            TreeViewNode treeViewNode = null;

            // if there are no args, the mouse didn't get clicked but we want to believe it did
            if (args != null)
            {
                treeViewNode = args.InvokedItem as TreeViewNode;
            }
            else
            {
                treeViewNode = treeView.SelectedNode;
            }

            // Grab the object that has Registry data in it from the currently selected treeView node
            RegistryKey registryKey = (RegistryKey)treeViewNode.Content;

            // no matter what happens, clear the ListView of items on each click
            ClearTable();

            // if there's no ListView items stored for the selected node, dataGrid is clear so get out now
            if (registryKey.Tag == null)
            {
                return;
            }

            // if there WAS something in the Tag property, cast it to a list and Populate the ListView
            ArrayList arrayList = (ArrayList)registryKey.Tag;
            m_listRegistryValues = new List<RegistryValue>();

            for (int i = 0; i < arrayList.Count; i++)
            {
                RegistryValue listViewItem = (RegistryValue)arrayList[i];
                m_listRegistryValues.Add(listViewItem);
            }

            // create a new binding for dataGrid and reattach it, updating the rows
            Binding ListRegistryValuesBinding = new Binding { Source = m_listRegistryValues };
            dataGrid.SetBinding(DataGrid.ItemsSourceProperty, ListRegistryValuesBinding);
        }

        /// <summary>
        /// When the text in textBox changes, reload treeView and possibly dataGrid and reset the save button
        /// </summary>   
        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshRegistryFile();
            saveButton.IsEnabled = true;
        }

        /// <summary>
        /// Readonly checkbox is checked, set textBox to read only; also update the font color so it has a hint of being "disabled"
        /// </summary>   
        private void checkBoxTextBox_Checked(object sender, RoutedEventArgs e)
        {
            textBox.IsReadOnly = true;
            textBox.Foreground = m_solidColorBrushReadOnly;
        }

        /// <summary>
        /// Readonly checkbox is unchecked, set textBox to be editable; also update the font color back to black
        /// </summary>   
        private void checkBoxTextBox_Unchecked(object sender, RoutedEventArgs e)
        {
            textBox.IsReadOnly = false;
            textBox.Foreground = m_solidColorBrushNormal;
        }
    }
}
