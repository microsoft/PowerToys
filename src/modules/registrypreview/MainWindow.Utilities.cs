using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Windows.Foundation.Metadata;

namespace RegistryPreview
{
    public sealed partial class MainWindow : Window
    {
        /// <summary>
        /// Method that opens and processes the passed in file name; expected to be an absolute path and a first time open
        /// </summary>   
        private bool OpenRegistryFile(string filename)
        {
            // Disable parts of the UI that can cause trouble when loading
            ChangeCursor(gridPreview, true);
            textBox.Text = "";

            // clear the treeView and dataGrid no matter what
            treeView.RootNodes.Clear();
            ClearTable();

            // update the current window's title with the current filename
            UpdateWindowTitle(filename);

            // Load in the whole file in one call and plop it all into textBox
            FileStream fileStream = null;
            try
            {
                FileStreamOptions fileStreamOptions = new FileStreamOptions();
                fileStreamOptions.Access = FileAccess.Read;
                fileStreamOptions.Share = FileShare.ReadWrite;
                fileStreamOptions.Mode = FileMode.Open;

                fileStream = new FileStream(filename, fileStreamOptions);
                StreamReader streamReader = new StreamReader(fileStream);

                String filenameText = streamReader.ReadToEnd();
                textBox.Text = filenameText;
                streamReader.Close();
            }
            catch
            {
                // restore TextChanged handler to make for clean UI
                textBox.TextChanged += textBox_TextChanged;

                // Reset the cursor but leave textBox disabled as no content got loaded
                ChangeCursor(gridPreview, false);
                return false;
            }
            finally 
            {
                // clean up no matter what
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
            }

            // now that the file is loaded and in textBox, parse the data
            ParseRegistryFile(textBox.Text);

            // Getting here means that the entire REG file was parsed without incident
            // so select the root of the tree and celebrate
            if (treeView.RootNodes.Count > 0)
            {
                treeView.SelectedNode = treeView.RootNodes[0];
                treeView.Focus(FocusState.Programmatic);
            }

            // reset the cursor
            ChangeCursor(gridPreview, false);
            return true;
        }

        /// <summary>
        /// Method that re-opens and processes the filename the app already knows about; expected to not be a first time open
        /// </summary>   
        private void RefreshRegistryFile()
        {
            // Disable parts of the UI that can cause trouble when loading
            ChangeCursor(gridPreview, true);

            // Get the current selected node so we can return focus to an existing node
            TreeViewNode currentNode = treeView.SelectedNode;

            // clear the treeView and dataGrid no matter what
            treeView.RootNodes.Clear();
            ClearTable();

            // the existing text is still in textBox so parse the data again
            ParseRegistryFile(textBox.Text);

            // check to see if there was a key in treeView before the refresh happened 
            if (currentNode != null)
            {
                // since there is a valid node, get the FullPath of the key that was selected
                String selectedFullPath = ((RegistryKey)currentNode.Content).FullPath;

                // check to see if we still have the key in the new Dictionary of keys
                if (m_mapRegistryKeys .ContainsKey(selectedFullPath))
                {
                    // we found it! select it in the tree and pretend it was selected
                    TreeViewNode treeViewNode;
                    m_mapRegistryKeys .TryGetValue(selectedFullPath, out treeViewNode);
                    treeView.SelectedNode = treeViewNode;
                    treeView_ItemInvoked(treeView, null);
                }
                else
                {
                    // we failed to find an existing node; it could have been deleted in the edit
                    if (treeView.RootNodes.Count > 0)
                    {
                        treeView.SelectedNode = treeView.RootNodes[0];
                    }
                }
            }
            else
            {
                // no node was previously selected so check for a RootNode and select it
                if (treeView.RootNodes.Count > 0)
                {
                    treeView.SelectedNode = treeView.RootNodes[0];
                }
            }

            // enable the UI
            ChangeCursor(gridPreview, false);
        }

        /// <summary>
        /// Parses the text that is passed in, which should be the same text that's in textBox
        /// </summary>   
        private bool ParseRegistryFile(string filenameText)
        {
            // if this is a not-first open, clear out the Dictionary of nodes
            if (m_mapRegistryKeys  != null)
            {
                m_mapRegistryKeys .Clear();
                m_mapRegistryKeys  = null;
            }

            // set up a new dictionary
            m_mapRegistryKeys  = new Dictionary<String, TreeViewNode>();

            // As we'll be processing the text one line at a time, this string will be the current line
            String registryLine;

            // Brute force editing: for textBox to show Cr-Lf corrected, we need to strip out the \n's
            filenameText = filenameText.Replace("\r\n", "\r");

            // split apart all of the text in textBox, where one element in the array represents one line
            String[] registryLines = filenameText.Split("\r");
            if (registryLines.Length <= 1)
            {
                // after the split, we have no lines so get out
                ChangeCursor(gridPreview, false);
                return false;
            }

            // REG files have to start with one of two headers and it's case insensitive
            registryLine = registryLines[0];
            registryLine = registryLine.ToLower();

            // make sure that this is a valid REG file, based on the first line of the file
            switch (registryLine)
            {
                case REGISTRY_HEADER_4:
                case REGISTRY_HEADER_5:
                    break;
                default:
                    ShowMessageBox(APP_NAME, App.s_Filename + m_resourceLoader.GetString("InvalidRegistryFile"), m_resourceLoader.GetString("OkButtonText"));
                    ChangeCursor(gridPreview, false);
                    return false;
            }

            // these are used for populating the tree as we read in one line at a time
            TreeViewNode treeViewNode = null;
            RegistryValue registryValue = null;

            // start with the first element of the array
            int index = 1;
            registryLine = registryLines[index];

            while (index < registryLines.Length)
            {
                // continue until we have nothing left to read
                // switch logic, based off what the current line we're reading is
                if (registryLine.StartsWith("["))
                {
                    // this is a key!
                    registryLine = registryLine.Replace("[", "");
                    registryLine = registryLine.Replace("]", "");

                    treeViewNode = AddTextToTree(registryLine);
                }
                else if ((registryLine.StartsWith("\"")) || (registryLine.StartsWith("@")))
                {
                    // this is a named value or default value (denoted with the @)

                    // split up the name from the value by looking for the first found =
                    int equal = registryLine.IndexOf('=');
                    if ((equal < 0) || (equal > registryLine.Length - 1))
                    {
                        // something is very wrong
                        Debug.WriteLine(String.Format("SOMETHING WENT WRONG: {0}", registryLine));
                        break;
                    }

                    // set the name and the value
                    String name = registryLine.Substring(0, equal);
                    name = name.Replace("\"", "");
                    String value = registryLine.Substring(equal + 1);

                    if (name == "@")
                    {
                        name = "(Default)";
                    }

                    // Create a new listview item that will be used to display the value
                    registryValue = new RegistryValue(name, "REG_SZ", "");

                    // if the first character is a " then this is a string value; get rid of the first and last "
                    if (value.StartsWith("\""))
                    {
                        value = value.Remove(0, 1);
                        // handles the case where someone is typing a new line with a REG_SZ value
                        if (value.Length > 0)
                        {
                            value = value.Substring(0, value.Length - 1);
                        }
                    }

                    // check the header of the Value's value and format it accordingly
                    if (value.StartsWith("dword:"))
                    {
                        registryValue.Type = "REG_DWORD";
                        value = value.Replace("dword:", "");
                    }
                    else if (value.StartsWith("hex(b):"))
                    {
                        registryValue.Type = "REG_QWORD";
                        value = value.Replace("hex(b):", "");
                    }
                    else if (value.StartsWith("hex:"))
                    {
                        registryValue.Type = "REG_BINARY";
                        value = value.Replace("hex:", "");
                    }
                    else if (value.StartsWith("hex(2):"))
                    {
                        registryValue.Type = "REG_EXAND_SZ";
                        value = value.Replace("hex(2):", "");
                    }
                    else if (value.StartsWith("hex(7):"))
                    {
                        registryValue.Type = "REG_MULTI_SZ";
                        value = value.Replace("hex(7):", "");
                    }
                    else
                    {
                        registryValue.Type = "REG_SZ";
                    }

                    // If the end of a decimal line ends in a \ then you have to keep
                    // reading the block as a single value!
                    while (value.EndsWith(@",\"))
                    {
                        value = value.TrimEnd('\\');
                        index++;
                        if (index >= registryLines.Length)
                        {
                            ChangeCursor(gridPreview, false);
                            return false;
                        }
                        registryLine = registryLines[index];
                        registryLine = registryLine.TrimStart();
                        value += registryLine;
                    }

                    // Clean out any escaped characters in the value, only for the preview
                    value = value.Replace("\\\\", "\\");    // Replace \\ with \ in the UI
                    value = value.Replace("\\\"", "\"");    // Replace \. with . in the UI

                    // update the ListViewItem with this information
                    registryValue.Value = value;

                    // We're going to store this ListViewItem in an ArrayList which will then
                    // be attached to the most recently returned TreeNode that came back from 
                    // AddTextToTree.  If there's already a list there, we will use that list and
                    // add our new node to it.
                    ArrayList arrayList = null;
                    if (((RegistryKey)treeViewNode.Content).Tag == null)
                    {
                        arrayList = new ArrayList();
                    }
                    else
                    {
                        arrayList = (ArrayList)(((RegistryKey)treeViewNode.Content).Tag);
                    }
                    arrayList.Add(registryValue);

                    // shove the updated array into the Tag property
                    ((RegistryKey)treeViewNode.Content).Tag = arrayList;
                }
                // if we get here, it's not a Key (starts with [) or Value (starts with " or @) so it's likely waste (comments that start with ; fall out here)

                // read the next line from the REG file
                index++;

                // if we've gone too far, escape the proc!
                if (index >= registryLines.Length)
                {
                    ChangeCursor(gridPreview, false);
                    return false;
                }

                // carry on with the next line
                registryLine = registryLines[index];
            }

            return true;
        }

        /// <summary>
        /// Adds the REG file that's being currently being viewed to the app's title bar
        /// </summary>   
        private void UpdateWindowTitle(String filename)
        {
            String[] file = filename.Split('\\');
            if (file.Length > 0)
            {
                m_appWindow.Title = file[file.Length - 1] + " - " + APP_NAME;
            }
            else
            {
                m_appWindow.Title = filename + " - " + APP_NAME;
            }
        }

        /// <summary>
        /// Helper method that assumes everything is enabled/disabled together
        /// </summary>   
        private void UpdateToolBarAndUI(bool enable)
        {
            UpdateToolBarAndUI(enable, enable, enable);
        }

        /// <summary>
        /// Enable command bar buttons and textBox.
        /// Note that writeButton, saveAsButton, and textBox all update with the same value on purpose
        /// </summary>   
        private void UpdateToolBarAndUI(bool enableWrite, bool enableRefresh, bool enableEdit)
        {
            refreshButton.IsEnabled = enableRefresh;
            editButton.IsEnabled = enableEdit;
            writeButton.IsEnabled = enableWrite;
            saveAsButton.IsEnabled = enableEdit; 
        }

        /// <summary>
        /// Helper method that creates a new TreeView node, attaches it to a parent if any, and then passes the new node back to the caller
        /// mapRegistryKeys is a collection of all of the [] lines in the file
        /// keys comes from the REG file and represents a bunch of nodes
        /// </summary>   
        private TreeViewNode AddTextToTree(String keys)
        {
            String[] individualKeys = keys.Split('\\');
            String fullPath = keys;
            TreeViewNode returnNewNode = null, newNode = null, previousNode = null;

            // Walk the list of keys backwards
            for (int i=individualKeys.Length - 1; i >= 0; i--)
            {
                // First check the dictionary, and return the current node if it already exists
                if (m_mapRegistryKeys .ContainsKey(fullPath))
                {
                    // was a new node created?
                    if (returnNewNode == null)
                    {
                        // if no new nodes have been created, send out the node we should have already
                        m_mapRegistryKeys .TryGetValue(fullPath, out returnNewNode);
                    }
                    else
                    {
                        // as a new node was created, hook it up to this found parent
                        m_mapRegistryKeys .TryGetValue(fullPath, out newNode);
                        newNode.Children.Add(previousNode);
                    }

                    // return the new node no matter what
                    return returnNewNode;
                }

                // Since the path is not in the tree, create a new node and add it to the dictionary
                RegistryKey registryKey = new RegistryKey(individualKeys[i], fullPath);
                newNode = new TreeViewNode() { Content = registryKey, IsExpanded = true };
                m_mapRegistryKeys .Add(fullPath, newNode);

                // if this is the first new node we're creating, we need to return it to the caller 
                if (previousNode == null)
                {
                    // capture the first node so it can be returned
                    returnNewNode = newNode;
                }
                else
                {
                    // The newly created node is a parent to the previously created node, as add it here.
                    newNode.Children.Add(previousNode);
                }

                // before moving onto the next node, tag the previous node and update the path
                previousNode = newNode;
                fullPath = fullPath.Replace(String.Format(@"\{0}", individualKeys[i]), "");
                
                // One last check: if we get here, the parent of this node is not yet in the tree, so we need to add it as a RootNode
                if (i == 0)
                {
                    treeView.RootNodes.Add(newNode);
                    treeView.UpdateLayout();
                }
            }
            return returnNewNode;
        }

        /// <summary>
        /// Wrapper method that shows a simple one-button message box, parented by the main application window
        /// </summary>
        private async void ShowMessageBox(String title, String content, String closeButtonText)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButtonText
            };

            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                contentDialog.XamlRoot = this.Content.XamlRoot;
            }
            await contentDialog.ShowAsync();
        }

        /// <summary>
        /// Wrapper method that shows a Save/Don't Save/Cancel message box, parented by the main application window and shown when closing the app
        /// </summary>
        private async void HandleDirtyClosing(String title, String content, String primaryButtonText, string secondaryButtonText, String closeButtonText)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText,
                CloseButtonText = closeButtonText,
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
                    // Save, then close
                    SaveFile();
                    break;
                case ContentDialogResult.Secondary:
                    // Don't save, and then close!
                    saveButton.IsEnabled = false;
                    break;
                default:
                    // Cancel closing!
                    return;
            }

            // if we got here, we should try to close again
            App.Current.Exit();
        }

        /// <summary>
        /// Method will open the Registry Editor or merge the current REG file into the Registry via the Editor
        /// Process will prompt for elevation of previledge if it needs it.
        /// </summary>   
        private void OpenRegistryEditor(string fileMerge)
        {
            Process process = new Process();
            process.StartInfo.FileName = "regedit.exe";
            process.StartInfo.UseShellExecute = true;
            if (File.Exists(fileMerge))
            {
                // If Merge was called, pass in the filename as a param to the Editor
                process.StartInfo.Arguments = String.Format("\"{0}\"", fileMerge);
            }

            try
            {
                process.Start();
            }
            catch
            {
                ShowMessageBox(
                    m_resourceLoader.GetString("UACDialogTitle"),
                    m_resourceLoader.GetString("UACDialogError"),
                    m_resourceLoader.GetString("OkButtonText")
                );
            }
        }

        /// <summary>
        /// Utility method that clears out the GridView as there's no other way to do it.
        /// </summary>   
        private void ClearTable()
        {
            if (m_listRegistryValues != null)
            {
                m_listRegistryValues.Clear();
            }

            dataGrid.ItemsSource = null;
        }

        /// <summary>
        /// Change the current app cursor at the grid level to be a wait cursor.  Sort of works, sort of doesn't, but it's a nice attempt.
        /// </summary>   
        public void ChangeCursor(UIElement uiElement, bool wait)
        {
            // You can only change the Cursor if the visual tree is loaded
            if (!m_visualTreeReady)
                return;

            InputCursor cursor = InputSystemCursor.Create(wait ? InputSystemCursorShape.Wait : InputSystemCursorShape.Arrow);
            System.Type type = typeof(UIElement);
            type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
        }

        /// <summary>
        /// Wrapper method that saves the current file in place, using the current text in textBox.
        /// </summary>
        private void SaveFile()
        {
            ChangeCursor(gridPreview, true);

            // set up the filesteam for all writing
            FileStream fileStream = null;

            try
            {
                // attempt to open the existing file for writing
                FileStreamOptions fileStreamOptions = new FileStreamOptions();
                fileStreamOptions.Access = FileAccess.Write;
                fileStreamOptions.Share = FileShare.Write;
                fileStreamOptions.Mode = FileMode.OpenOrCreate;

                fileStream = new FileStream(App.s_Filename, fileStreamOptions);
                StreamWriter streamWriter = new StreamWriter(fileStream);

                // if we get here, the file is open and writable so dump the whole contents of textBox
                String filenameText = textBox.Text;
                streamWriter.Write(filenameText);
                streamWriter.Flush();
                streamWriter.Close();

                // only change when the save is successful
                saveButton.IsEnabled = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                // this exception is thrown if the file is there but marked as read only
                ShowMessageBox(
                    m_resourceLoader.GetString("ErrorDialogTitle"),
                    ex.Message,
                    m_resourceLoader.GetString("OkButtonText")
                );
            }
            catch
            {
                // this catch handles all other excpetions thrown when trying to write the file out
                ShowMessageBox(
                    m_resourceLoader.GetString("ErrorDialogTitle"),
                    m_resourceLoader.GetString("FileSaveError"),
                    m_resourceLoader.GetString("OkButtonText")
                );
            }
            finally 
            {
                // clean up no matter what
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
            }

            // restore the cursor
            ChangeCursor(gridPreview, false);
        }
    }
}
