// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.ViewModels;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace ImageResizer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private const string LogSubFolder = "\\Image Resizer\\Logs";

        /// <summary>
        /// Gets cached AI availability state, checked at app startup.
        /// Can be updated after model download completes or background initialization.
        /// </summary>
        public static AiAvailabilityState AiAvailabilityState { get; internal set; }

        /// <summary>
        /// Event fired when AI initialization completes in background.
        /// Allows UI to refresh state when initialization finishes.
        /// </summary>
        public static event EventHandler<AiAvailabilityState> AiInitializationCompleted;

        private Window _window;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Language initialization error: " + ex.Message);
            }

            try
            {
                Logger.InitializeLogger(LogSubFolder);
            }
            catch
            {
                // Swallow logger init issues silently
            }

            Console.InputEncoding = Encoding.Unicode;

            this.InitializeComponent();

            UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Initialize dispatcher for cross-thread property change notifications
            Settings.InitializeDispatcher();

            // Check GPO policy
            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                Environment.Exit(0);
                return;
            }

            // Check for AI detection mode (called by Runner in background)
            var commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs?.Length > 1 && commandLineArgs[1] == "--detect-ai")
            {
                RunAiDetectionMode();
                return;
            }

            // Initialize AI availability
            InitializeAiAvailability();

            // Create batch from command line
            var batch = ResizeBatch.FromCommandLine(Console.In, commandLineArgs);

            // Create main window (not yet visible – HWND is available for the file picker)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            _window = mainWindow;

            mainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                if (batch.Files.Count == 0)
                {
                    // Show file picker before the window is visible
                    var files = await mainWindow.OpenPictureFilesAsync();
                    if (!files.Any())
                    {
                        Environment.Exit(0);
                        return;
                    }

                    foreach (var file in files)
                    {
                        batch.Files.Add(file);
                    }
                }

                // Load ViewModel (sets page content) then activate so the window appears with content ready
                await mainWindow.LoadViewModelAsync();
                mainWindow.Activate();
            });
        }

        private void InitializeAiAvailability()
        {
            // AI Super Resolution is currently disabled
            AiAvailabilityState = AiAvailabilityState.NotSupported;
            ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);

            // If AI is enabled in the future, uncomment this section:
            /*
            // AI Super Resolution is not supported on Windows 10
            if (OSVersionHelper.IsWindows10())
            {
                AiAvailabilityState = AiAvailabilityState.NotSupported;
                ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                Logger.LogInfo("AI Super Resolution not supported on Windows 10");
            }
            else
            {
                // Load AI availability from cache
                var cachedState = AiAvailabilityCacheService.LoadCache();

                if (cachedState.HasValue)
                {
                    AiAvailabilityState = cachedState.Value;
                    Logger.LogInfo($"AI state loaded from cache: {AiAvailabilityState}");
                }
                else
                {
                    AiAvailabilityState = AiAvailabilityState.NotSupported;
                    Logger.LogInfo("No AI cache found, defaulting to NotSupported");
                }

                // If AI is potentially available, start background initialization
                if (AiAvailabilityState == AiAvailabilityState.Ready)
                {
                    _ = InitializeAiServiceAsync();
                }
                else
                {
                    ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                }
            }
            */
        }

        /// <summary>
        /// AI detection mode: perform detection, write to cache, and exit.
        /// </summary>
        private void RunAiDetectionMode()
        {
            try
            {
                Logger.LogInfo("Running AI detection mode...");

                // AI is currently disabled
                AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
                Logger.LogInfo("AI detection complete: NotSupported (feature disabled)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"AI detection failed: {ex.Message}");
                AiAvailabilityCacheService.SaveCache(AiAvailabilityState.NotSupported);
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Initialize AI Super Resolution service asynchronously in background.
        /// </summary>
        private static async Task InitializeAiServiceAsync()
        {
            AiAvailabilityState finalState;

            try
            {
                var aiService = await WinAiSuperResolutionService.CreateAsync();

                if (aiService != null)
                {
                    ResizeBatch.SetAiSuperResolutionService(aiService);
                    Logger.LogInfo("AI Super Resolution service initialized successfully.");
                    finalState = AiAvailabilityState.Ready;
                }
                else
                {
                    ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                    Logger.LogWarning("AI Super Resolution service initialization failed. Using default service.");
                    finalState = AiAvailabilityState.NotSupported;
                }
            }
            catch (Exception ex)
            {
                ResizeBatch.SetAiSuperResolutionService(NoOpAiSuperResolutionService.Instance);
                Logger.LogError($"Exception during AI service initialization: {ex.Message}");
                finalState = AiAvailabilityState.NotSupported;
            }

            AiAvailabilityState = finalState;
            AiInitializationCompleted?.Invoke(null, finalState);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        public void Dispose()
        {
            ResizeBatch.DisposeAiSuperResolutionService();
            GC.SuppressFinalize(this);
        }
    }
}
