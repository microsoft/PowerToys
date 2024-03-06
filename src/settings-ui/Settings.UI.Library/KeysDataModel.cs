// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels.Commands;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeysDataModel : INotifyPropertyChanged
    {
        [JsonPropertyName("originalKeys")]
        public string OriginalKeys { get; set; }

        [JsonPropertyName("secondKeyOfChord")]
        public uint SecondKeyOfChord { get; set; }

        [JsonPropertyName("newRemapKeys")]
        public string NewRemapKeys { get; set; }

        [JsonPropertyName("unicodeText")]
        public string NewRemapString { get; set; }

        [JsonPropertyName("runProgramFilePath")]
        public string RunProgramFilePath { get; set; }

        [JsonPropertyName("runProgramArgs")]
        public string RunProgramArgs { get; set; }

        [JsonPropertyName("openUri")]
        public string OpenUri { get; set; }

        [JsonPropertyName("operationType")]
        public int OperationType { get; set; }

        private enum KeyboardManagerEditorType
        {
            KeyEditor = 0,
            ShortcutEditor,
        }

        public const string CommaSeparator = "<comma>";

        private static Process editor;
        private ICommand _editShortcutItemCommand;

        public ICommand EditShortcutItem => _editShortcutItemCommand ?? (_editShortcutItemCommand = new RelayCommand<object>(OnEditShortcutItem));

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void OnEditShortcutItem(object parameter)
        {
            OpenEditor((int)KeyboardManagerEditorType.ShortcutEditor);
        }

        private async void OpenEditor(int type)
        {
            if (editor != null)
            {
                BringProcessToFront(editor);
                return;
            }

            const string PowerToyName = KeyboardManagerSettings.ModuleName;
            const string KeyboardManagerEditorPath = "KeyboardManagerEditor\\PowerToys.KeyboardManagerEditor.exe";
            try
            {
                if (editor != null && editor.HasExited)
                {
                    Logger.LogInfo($"Previous instance of {PowerToyName} editor exited");
                    editor = null;
                }

                if (editor != null)
                {
                    Logger.LogInfo($"The {PowerToyName} editor instance {editor.Id} exists. Bringing the process to the front");
                    BringProcessToFront(editor);
                    return;
                }

                string path = Path.Combine(Environment.CurrentDirectory, KeyboardManagerEditorPath);
                Logger.LogInfo($"Starting {PowerToyName} editor from {path}");

                // InvariantCulture: type represents the KeyboardManagerEditorType enum value
                editor = Process.Start(path, $"{type.ToString(CultureInfo.InvariantCulture)} {Environment.ProcessId}");

                await editor.WaitForExitAsync();

                editor = null;
            }
            catch (Exception e)
            {
                editor = null;
                Logger.LogError($"Exception encountered when opening an {PowerToyName} editor", e);
            }
        }

        private static void BringProcessToFront(Process process)
        {
            if (process == null)
            {
                return;
            }

            IntPtr handle = process.MainWindowHandle;
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SWRESTORE);
            }

            NativeMethods.SetForegroundWindow(handle);
        }

        private static List<string> MapKeysOnlyChord(uint secondKeyOfChord)
        {
            var result = new List<string>();
            if (secondKeyOfChord <= 0)
            {
                return result;
            }

            result.Add(Helper.GetKeyName(secondKeyOfChord));

            return result;
        }

        private static List<string> MapKeys(string stringOfKeys, uint secondKeyOfChord, bool splitChordsWithComma = false)
        {
            if (stringOfKeys == null)
            {
                return new List<string>();
            }

            if (secondKeyOfChord > 0)
            {
                var keys = stringOfKeys.Split(';');
                return keys.Take(keys.Length - 1)
                    .Select(uint.Parse)
                    .Select(Helper.GetKeyName)
                    .ToList();
            }
            else
            {
                if (splitChordsWithComma)
                {
                    var keys = stringOfKeys.Split(';')
                        .Select(uint.Parse)
                        .Select(Helper.GetKeyName)
                        .ToList();
                    keys.Insert(keys.Count - 1, CommaSeparator);
                    return keys;
                }
                else
                {
                    return stringOfKeys
                    .Split(';')
                    .Select(uint.Parse)
                    .Select(Helper.GetKeyName)
                    .ToList();
                }
            }
        }

        private static List<string> MapKeys(string stringOfKeys)
        {
            return MapKeys(stringOfKeys, 0);
        }

        public List<string> GetMappedOriginalKeys(bool ignoreSecondKeyInChord, bool splitChordsWithComma = false)
        {
            if (ignoreSecondKeyInChord && SecondKeyOfChord > 0)
            {
                return MapKeys(OriginalKeys, SecondKeyOfChord);
            }
            else
            {
                return MapKeys(OriginalKeys, 0, splitChordsWithComma);
            }
        }

        public List<string> GetMappedOriginalKeysOnlyChord()
        {
            return MapKeysOnlyChord(SecondKeyOfChord);
        }

        public List<string> GetMappedOriginalKeys()
        {
            return GetMappedOriginalKeys(false);
        }

        public List<string> GetMappedOriginalKeysWithSplitChord()
        {
            return GetMappedOriginalKeys(false, true);
        }

        public bool IsRunProgram
        {
            get
            {
                return OperationType == 1;
            }
        }

        public bool IsOpenURI
        {
            get
            {
                return OperationType == 2;
            }
        }

        public bool IsOpenUriOrIsRunProgram
        {
            get
            {
                return IsOpenURI || IsRunProgram;
            }
        }

        public bool HasChord
        {
            get
            {
                return SecondKeyOfChord > 0;
            }
        }

        public List<string> GetMappedNewRemapKeys(int runProgramMaxLength)
        {
            if (IsRunProgram)
            {
                // we're going to just pretend this is a "key" if we have a RunProgramFilePath
                if (string.IsNullOrEmpty(RunProgramFilePath))
                {
                    return new List<string>();
                }
                else
                {
                    return new List<string> { FormatFakeKeyForDisplay(runProgramMaxLength) };
                }
            }
            else if (IsOpenURI)
            {
                // we're going to just pretend this is a "key" if we have a RunProgramFilePath
                if (string.IsNullOrEmpty(OpenUri))
                {
                    return new List<string>();
                }
                else
                {
                    if (OpenUri.Length > runProgramMaxLength)
                    {
                        return new List<string> { $"{OpenUri.Substring(0, runProgramMaxLength - 3)}..." };
                    }
                    else
                    {
                        return new List<string> { OpenUri };
                    }
                }
            }

            return (string.IsNullOrEmpty(NewRemapString) || NewRemapString == "*Unsupported*") ? MapKeys(NewRemapKeys) : new List<string> { NewRemapString };
        }

        // Instead of doing something fancy pants, we 'll just display the RunProgramFilePath data when it's IsRunProgram
        // It truncates the start of the program to run, if it's long and truncates the end of the args if it's long
        // e.g.: c:\MyCool\PathIs\Long\software.exe myArg1 myArg2 myArg3 -> (something like) "...ng\software.exe myArg1..."
        // the idea is you get the most important part of the program to run and some of the args in case that the only thing thats different,
        // e.g: "...path\software.exe cool1.txt" and "...path\software.exe cool3.txt"
        private string FormatFakeKeyForDisplay(int runProgramMaxLength)
        {
            // was going to use this:
            var fakeKey = Environment.ExpandEnvironmentVariables(RunProgramFilePath);
            try
            {
                if (File.Exists(fakeKey))
                {
                    fakeKey = Path.GetFileName(Environment.ExpandEnvironmentVariables(RunProgramFilePath));
                }
            }
            catch
            {
            }

            fakeKey = $"{fakeKey} {RunProgramArgs}".Trim();

            if (fakeKey.Length > runProgramMaxLength)
            {
                fakeKey = $"{fakeKey.Substring(0, runProgramMaxLength - 3)}...";
            }

            return fakeKey;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
