// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Converters;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VirtualKey = Windows.System.VirtualKey;
using XamlMetaDataProvider = Microsoft.PowerToys.Settings.UI.PowerToys_Settings_XamlTypeInfo.XamlMetaDataProvider;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Controls
{
    [TestClass]
    [DoNotParallelize]
    public partial class ShortcutControlTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private static readonly FieldInfo RepositoryField = typeof(SettingsRepository<GeneralSettings>).GetField("settingsRepository", BindingFlags.Static | BindingFlags.NonPublic);
        private static DispatcherQueue dispatcher;
        private static Thread uiThread;
        private static object previousRepository;

        [ClassInitialize]
        public static async Task Initialize(TestContext context)
        {
            // Use an in-memory repository before the conflict helper is initialized.
            // No Settings app, settings files, IPC connection or visible window is needed.
            previousRepository = RepositoryField.GetValue(null);
            var repository = (SettingsRepository<GeneralSettings>)Activator.CreateInstance(typeof(SettingsRepository<GeneralSettings>), nonPublic: true);
            repository.SettingsConfig = new GeneralSettings();
            RepositoryField.SetValue(null, repository);

            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            uiThread = new Thread(() =>
            {
                try
                {
                    // Initialize the referenced Settings runtime before calling into WinUI.
                    RuntimeHelpers.RunModuleConstructor(typeof(ShortcutControl).Module.ModuleHandle);
                    WinRT.ComWrappersSupport.InitializeComWrappers();
                    Application.Start(initializationParameters =>
                    {
                        try
                        {
                            _ = new ShortcutTestApplication(ready);
                            dispatcher = DispatcherQueue.GetForCurrentThread();
                        }
                        catch (Exception ex)
                        {
                            ready.SetException(ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    ready.TrySetException(ex);
                }
            });
            uiThread.IsBackground = true;
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            await ready.Task.WaitAsync(Timeout);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            if (dispatcher != null)
            {
                Assert.IsTrue(dispatcher.TryEnqueue(() => Application.Current.Exit()));
                Assert.IsTrue(uiThread.Join(Timeout), "The test XAML thread did not exit.");
            }

            RepositoryField.SetValue(null, previousRepository);
        }

        [TestMethod]
        public Task SummaryAndReloadDoNotConstructEditor()
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl
                {
                    Enabled = true,
                    AllowDisable = true,
                    HotkeySettings = new HotkeySettings(true, false, false, false, 38),
                };

                var settings = control.HotkeySettings;
                for (var i = 0; i < 3; i++)
                {
                    Invoke(control, "ShortcutControl_Loaded", control, null);
                    settings.ConflictDescription = $"Conflict {i}";
                    settings.HasConflict = true;
                    Assert.AreEqual(settings.ConflictDescription, control.Tooltip);
                    Assert.IsTrue(control.HasConflict);
                    Assert.IsNull(Field<ShortcutDialogContentControl>(control, "c"));
                    Assert.IsNull(Field<ContentDialog>(control, "shortcutDialog"));
                    Invoke(control, "ShortcutControl_Unloaded", control, null);
                    settings.HasConflict = false;
                    Assert.IsTrue(control.HasConflict, "Unloaded controls should not observe settings changes.");
                }

                Invoke(control, "ShortcutControl_Loaded", control, null);
                Assert.IsFalse(control.HasConflict, "Reload should refresh changes made while unloaded.");
            });
        }

        [TestMethod]
        public Task FirstOpenPrimesCurrentStateAndReusesEditor()
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl
                {
                    AllowDisable = true,
                    HotkeySettings = new HotkeySettings(false, true, true, false, 38)
                    {
                        HasConflict = true,
                        ConflictDescription = "A conflicting shortcut",
                    },
                    IgnoreConflict = true,
                    RequestedTheme = ElementTheme.Dark,
                };

                Invoke(control, "PrepareEditor");
                var content = Field<ShortcutDialogContentControl>(control, "c");
                var dialog = Field<ContentDialog>(control, "shortcutDialog");
                Assert.AreSame(content, dialog.Content);
                CollectionAssert.AreEqual(control.HotkeySettings.GetKeysList(), content.Keys);
                Assert.IsTrue(content.HasConflict);
                Assert.IsTrue(content.IgnoreConflict);
                Assert.AreEqual(control.HotkeySettings.ConflictDescription, content.ConflictMessage);
                Assert.IsTrue(content.IsWarningAltGr);
                Assert.AreEqual(control.ActualTheme, dialog.RequestedTheme);
                Assert.AreSame(control.HotkeySettings, Field<HotkeySettings>(control, "lastValidSettings"));
                Assert.AreEqual(
                    ResourceLoaderInstance.ResourceLoader.GetString("Activation_Shortcut_With_Disable_Description"),
                    content.FindDescendant<TextBlock>().Text);

                // Discard a partially entered chord and use updated settings on reopening.
                Field<HotkeySettings>(control, "internalSettings").Ctrl = true;
                control.HotkeySettings = new HotkeySettings(true, false, false, false, 40);
                control.AllowDisable = false;
                control.RequestedTheme = ElementTheme.Light;
                Invoke(control, "PrepareEditor");
                Assert.AreSame(content, Field<ShortcutDialogContentControl>(control, "c"));
                Assert.AreSame(dialog, Field<ContentDialog>(control, "shortcutDialog"));
                Assert.IsTrue(Field<HotkeySettings>(control, "internalSettings").IsEmpty());
                Assert.AreSame(control.HotkeySettings, Field<HotkeySettings>(control, "lastValidSettings"));
                CollectionAssert.AreEqual(control.HotkeySettings.GetKeysList(), content.Keys);
                Assert.IsFalse(content.HasConflict);
                Assert.IsFalse(content.IsWarningAltGr);
                Assert.AreEqual(control.ActualTheme, dialog.RequestedTheme);
                Assert.AreEqual(
                    ResourceLoaderInstance.ResourceLoader.GetString("Activation_Shortcut_Description"),
                    content.FindDescendant<TextBlock>().Text);
            });
        }

        [TestMethod]
        public Task EditorHandlersSurviveReloadWithoutDuplicates()
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl();
                Invoke(control, "PrepareEditor");
                var content = Field<ShortcutDialogContentControl>(control, "c");

                for (var i = 0; i < 3; i++)
                {
                    Invoke(control, "ShortcutControl_Loaded", control, null);
                    Invoke(control, "ShortcutControl_Unloaded", control, null);
                    Invoke(control, "PrepareEditor");

                    Assert.AreEqual(1, Field<Delegate>(content, "ResetClick").GetInvocationList().Length);
                    Assert.AreEqual(1, Field<Delegate>(content, "ClearClick").GetInvocationList().Length);
                    Assert.AreEqual(1, Field<Delegate>(content, "LearnMoreClick").GetInvocationList().Length);
                }
            });
        }

        [TestMethod]
        public Task EmptyAndMissingShortcutsCanPrimeEditor()
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl();
                Invoke(control, "PrepareEditor");
                var content = Field<ShortcutDialogContentControl>(control, "c");
                Assert.AreEqual(0, content.Keys.Count);
                Assert.IsFalse(content.IsWarningAltGr);
                Assert.IsFalse(content.HasConflict);
                Invoke(control, "ShortcutDialog_Opened", null, null);
                Assert.IsFalse(Field<ContentDialog>(control, "shortcutDialog").IsPrimaryButtonEnabled);
                Invoke(control, "ShortcutDialog_Closing", null, null);

                control.HotkeySettings = new HotkeySettings();
                Invoke(control, "PrepareEditor");
                Assert.AreEqual(0, content.Keys.Count);
                Assert.AreSame(control.HotkeySettings, Field<HotkeySettings>(control, "lastValidSettings"));
                Invoke(control, "ShortcutDialog_Opened", null, null);
                Assert.IsTrue(Field<ContentDialog>(control, "shortcutDialog").IsPrimaryButtonEnabled);
                Assert.IsFalse(content.IsError);
                Invoke(control, "ShortcutDialog_Closing", null, null);
            });
        }

        [TestMethod]
        [DataRow(false, true, true)]
        [DataRow(true, true, false)]
        [DataRow(false, false, false)]
        public Task ModifierCaptureAndAltGrWarningWorkAfterLazyCreation(bool win, bool alt, bool warning)
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl
                {
                    HotkeySettings = new HotkeySettings(win, true, alt, false, (int)VirtualKey.Up),
                };
                Invoke(control, "PrepareEditor");
                var content = Field<ShortcutDialogContentControl>(control, "c");
                Invoke(control, "Hotkey_KeyDown", (int)VirtualKey.Control);
                if (win)
                {
                    Invoke(control, "Hotkey_KeyDown", (int)VirtualKey.LeftWindows);
                }

                if (alt)
                {
                    Invoke(control, "Hotkey_KeyDown", (int)VirtualKey.Menu);
                }

                Invoke(control, "Hotkey_KeyDown", (int)VirtualKey.Up);
                Assert.AreEqual(warning, content.IsWarningAltGr);
                Assert.IsFalse(content.IsError);
                Assert.IsTrue(Field<ContentDialog>(control, "shortcutDialog").IsPrimaryButtonEnabled);
                CollectionAssert.AreEqual(control.HotkeySettings.GetKeysList(), content.Keys);

                Invoke(control, "Hotkey_KeyUp", (int)VirtualKey.Up);
                Invoke(control, "Hotkey_KeyUp", (int)VirtualKey.Control);
                Invoke(control, "Hotkey_KeyUp", (int)VirtualKey.Menu);
                Invoke(control, "Hotkey_KeyUp", (int)VirtualKey.LeftWindows);
                Assert.IsTrue(Field<HotkeySettings>(control, "internalSettings").IsEmpty());
            });
        }

        [TestMethod]
        public Task ReopeningDiscardsCancelledInputAndSavesCurrentShortcut()
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl
                {
                    HotkeySettings = new HotkeySettings(true, false, false, false, 38),
                };
                Invoke(control, "PrepareEditor");
                var original = control.HotkeySettings;
                typeof(ShortcutControl).GetField("lastValidSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(control, new HotkeySettings(false, true, false, false, 40));
                Invoke(control, "ShortcutDialog_Closing", null, null);
                Assert.AreSame(original, control.HotkeySettings);

                control.HotkeySettings = new HotkeySettings(false, false, true, false, 37);
                var updated = control.HotkeySettings.GetKeysList();
                Invoke(control, "PrepareEditor");
                Invoke(control, "ShortcutDialog_Opened", null, null);
                Assert.IsTrue(Field<ContentDialog>(control, "shortcutDialog").IsPrimaryButtonEnabled);
                Invoke(control, "ShortcutDialog_PrimaryButtonClick", null, null);
                Invoke(control, "ShortcutDialog_Closing", null, null);
                CollectionAssert.AreEqual(updated, control.HotkeySettings.GetKeysList());
            });
        }

        [TestMethod]
        [DataRow("C_ResetClick")]
        [DataRow("C_ClearClick")]
        public Task ResetAndClearKeepObservingTheReplacementSettings(string handler)
        {
            return RunOnUIThread(() =>
            {
                using var control = new ShortcutControl
                {
                    HotkeySettings = new HotkeySettings(true, false, false, false, 38),
                };
                var previous = control.HotkeySettings;
                Invoke(control, "PrepareEditor");
                Invoke(control, handler, null, null);
                Assert.IsTrue(control.HotkeySettings.IsEmpty());

                previous.HasConflict = true;
                Assert.IsFalse(control.HasConflict);
                control.HotkeySettings.HasConflict = true;
                Assert.IsTrue(control.HasConflict);
            });
        }

        private static async Task RunOnUIThread(Action action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.IsTrue(dispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }));
            await completion.Task.WaitAsync(Timeout);
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static void Invoke(object instance, string name, params object[] arguments)
        {
            instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
        }

        private sealed partial class ShortcutTestApplication : Application, IXamlMetadataProvider
        {
            private readonly XamlMetaDataProvider metadata = new XamlMetaDataProvider();
            private readonly TaskCompletionSource ready;

            public ShortcutTestApplication(TaskCompletionSource ready)
            {
                this.ready = ready;
            }

            protected override void OnLaunched(LaunchActivatedEventArgs args)
            {
                try
                {
                    InitializeResources();
                    ready.SetResult();
                }
                catch (Exception ex)
                {
                    ready.SetException(new InvalidOperationException($"Test XAML initialization failed (0x{ex.HResult:X8}).", ex));
                }
            }

            private void InitializeResources()
            {
                Resources = new ResourceDictionary();
                Resources.MergedDictionaries.Add(new XamlControlsResources());
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///PowerToys.Common.UI.Controls/Controls/KeyVisual/KeyVisual.xaml") });
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///PowerToys.Common.UI.Controls/Controls/KeyVisual/KeyCharPresenter.xaml") });
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///PowerToys.Common.UI.Controls/Controls/IsEnabledTextBlock/IsEnabledTextBlock.xaml") });
                Resources["SettingActionControlMinWidth"] = 240.0;
                Resources["DoubleToVisibilityConverter"] = new DoubleToVisibilityConverter { GreaterThan = 0, TrueValue = Visibility.Visible, FalseValue = Visibility.Collapsed };
                Resources["DoubleToInvertedVisibilityConverter"] = new DoubleToVisibilityConverter { GreaterThan = 0, TrueValue = Visibility.Collapsed, FalseValue = Visibility.Visible };
            }

            public IXamlType GetXamlType(Type type) => metadata.GetXamlType(type);

            public IXamlType GetXamlType(string fullName) => metadata.GetXamlType(fullName);

            public XmlnsDefinition[] GetXmlnsDefinitions() => metadata.GetXmlnsDefinitions();
        }
    }
}
