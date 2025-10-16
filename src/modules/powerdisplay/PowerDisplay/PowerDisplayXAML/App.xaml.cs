// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using PowerDisplay.Helpers;
using PowerToys.Interop;

namespace PowerDisplay
{
    /// <summary>
    /// PowerDisplay 应用程序主类
    /// </summary>
    public partial class App : Application
    {
        private Window? _mainWindow;
        private int _powerToysRunnerPid;
        private static Mutex? _mutex;

        /// <summary>
        /// 初始化 PowerDisplay 应用程序
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Initialize Logger
            Logger.InitializeLogger("\\PowerDisplay\\Logs");

            // Initialize PowerToys telemetry
            try
            {
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.Events.PowerDisplayStartEvent());
            }
            catch
            {
                // Telemetry errors should not crash the app
            }

            // Initialize language settings
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
            }

            // 应用保存的主题设置（优先从PowerToys设置读取）
            var savedTheme = ThemeManager.GetSavedThemeWithPriority();
            if (savedTheme != ElementTheme.Default)
            {
                // 转换ElementTheme到ApplicationTheme
                this.RequestedTheme = savedTheme == ElementTheme.Light
                    ? ApplicationTheme.Light
                    : ApplicationTheme.Dark;
            }

            // 处理未捕获的异常
            this.UnhandledException += OnUnhandledException;
        }
        
        /// <summary>
        /// 处理未捕获的异常
        /// </summary>
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // 尝试显示错误信息
            ShowStartupError(e.Exception);

            // 标记异常已处理，防止应用崩溃
            e.Handled = true;
        }

        /// <summary>
        /// 在应用程序启动时调用
        /// </summary>
        /// <param name="args">启动参数</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 使用 Mutex 确保只有一个 PowerDisplay 实例运行
                _mutex = new Mutex(true, "PowerDisplay", out bool isNewInstance);

                if (!isNewInstance)
                {
                    // PowerDisplay 已经在运行，退出当前实例
                    Logger.LogInfo("PowerDisplay is already running. Exiting duplicate instance.");
                    Environment.Exit(0);
                    return;
                }

                // 确保在应用退出时释放 Mutex
                AppDomain.CurrentDomain.ProcessExit += (_, _) => _mutex?.ReleaseMutex();

                // 解析命令行参数
                var cmdArgs = Environment.GetCommandLineArgs();
                if (cmdArgs?.Length > 1)
                {
                    // 支持两种格式：直接PID或者 --pid PID
                    int pidValue = -1;

                    // 检查是否是 --pid 格式
                    for (int i = 1; i < cmdArgs.Length - 1; i++)
                    {
                        if (cmdArgs[i] == "--pid" && int.TryParse(cmdArgs[i + 1], out pidValue))
                        {
                            break;
                        }
                    }

                    // 如果不是 --pid 格式，尝试解析最后一个参数（兼容旧格式）
                    if (pidValue == -1 && cmdArgs.Length > 1)
                    {
                        int.TryParse(cmdArgs[cmdArgs.Length - 1], out pidValue);
                    }

                    if (pidValue > 0)
                    {
                        _powerToysRunnerPid = pidValue;

                        // 从PowerToys Runner启动
                        Logger.LogInfo($"PowerDisplay started from PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                        // 监控父进程
                        RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                        {
                            Logger.LogInfo("PowerToys Runner exited. Exiting PowerDisplay");
                            ForceExit();
                        });
                    }
                }
                else
                {
                    // 独立运行模式
                    Logger.LogInfo("PowerDisplay started detached from PowerToys Runner.");
                    _powerToysRunnerPid = -1;
                }

                // 创建主窗口但不激活，窗口会在初始化后自动隐藏
                _mainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                ShowStartupError(ex);
            }
        }

        /// <summary>
        /// 显示启动错误
        /// </summary>
        private void ShowStartupError(Exception ex)
        {
            try
            {
                var errorWindow = new Window
                {
                    Title = "PowerDisplay - 启动错误"
                };

                var rootPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16
                };

                var titleText = new TextBlock
                {
                    Text = "PowerDisplay 启动失败",
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };

                var messageText = new TextBlock
                {
                    Text = $"错误信息：{ex.Message}",
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };

                var detailsText = new TextBlock
                {
                    Text = $"详细信息：\n{ex}",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var closeButton = new Button
                {
                    Content = "关闭",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                closeButton.Click += (_, _) => errorWindow.Close();

                rootPanel.Children.Add(titleText);
                rootPanel.Children.Add(messageText);
                rootPanel.Children.Add(detailsText);
                rootPanel.Children.Add(closeButton);

                var scrollViewer = new ScrollViewer
                {
                    Content = rootPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 600,
                    MaxWidth = 800
                };

                errorWindow.Content = scrollViewer;
                errorWindow.Activate();
            }
            catch
            {
                // 如果连错误窗口都无法显示，静默退出
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// 获取主窗口实例
        /// </summary>
        public Window? MainWindow => _mainWindow;

        /// <summary>
        /// 判断是否独立运行（不是从PowerToys Runner启动）
        /// </summary>
        public bool IsRunningDetachedFromPowerToys()
        {
            return _powerToysRunnerPid == -1;
        }

        /// <summary>
        /// 应用程序退出时的快速清理
        /// </summary>
        public void Shutdown()
        {
            try
            {
                // 启动超时机制，确保1秒内必须退出
                var timeoutTimer = new System.Threading.Timer(_ =>
                {
                    Logger.LogWarning("Shutdown timeout reached, forcing exit");
                    Environment.Exit(0);
                }, null, 1000, System.Threading.Timeout.Infinite);

                // 立即通知 MainWindow 程序正在退出，启用快速退出模式
                if (_mainWindow is MainWindow mainWindow)
                {
                    mainWindow.SetExiting();
                    mainWindow.FastShutdown(); // 新增快速关闭方法
                }

                _mainWindow = null;

                // 立即释放 Mutex
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
                _mutex = null;

                // 取消超时计时器
                timeoutTimer?.Dispose();
            }
            catch
            {
                // 忽略清理错误，确保能够退出
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 强制退出应用程序，确保完全终止
        /// </summary>
        private void ForceExit()
        {
            try
            {
                // 立即启动超时机制，500ms内必须退出
                var emergencyTimer = new System.Threading.Timer(_ =>
                {
                    Logger.LogWarning("Emergency exit timeout reached, terminating process");
                    Environment.Exit(0);
                }, null, 500, System.Threading.Timeout.Infinite);

                PerformForceExit();
            }
            catch
            {
                // 如果所有其他方法都失败，立即强制退出进程
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// 执行快速退出操作
        /// </summary>
        private void PerformForceExit()
        {
            try
            {
                // 快速关闭
                Shutdown();

                // 立即退出
                Environment.Exit(0);
            }
            catch
            {
                // 确保能够退出
                Environment.Exit(0);
            }
        }
    }
}
