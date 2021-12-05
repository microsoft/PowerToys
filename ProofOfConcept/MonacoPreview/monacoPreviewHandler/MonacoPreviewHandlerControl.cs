using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using Windows.UI.Core;
using Common;
using Common.ComInterlop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using monacoPreview;
using Nito.AsyncEx.Synchronous;
using PreviewHandlerCommon;
using WK.Libraries.WTL;

namespace MonacoPreviewHandler
{
    public class MonacoPreviewHandlerControl : FormHandlerControl
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        // This variable prevents users from navigating
        private bool WasNavigated = false;

        // Settings from Settings.cs
        private readonly Settings settings = new Settings();

        // Filehandler class from FileHandler.cs
        private readonly FileHandler fileHandler = new FileHandler();

        public Microsoft.Web.WebView2.WinForms.WebView2 webView;
        private CoreWebView2Environment webView2Environment;

        private Label _loading;
        
        public MonacoPreviewHandlerControl()
        {
        }
        [STAThread]
        public override void DoPreview<T>(T dataSource)
        {
            InitializeLoadingScreen();
            webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            if (!(dataSource is string filePath))
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandler)} must be a string but was a '{typeof(T)}'");
            }
            

            
            string[] file = GetFile(filePath);
            try
            {
                // WebView2 in separate thread:
                InvokeOnControlThread( () =>
                {
                    ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter webView2EnvironmentAwaiter = CoreWebView2Environment.CreateAsync(userDataFolder: System.Environment.GetEnvironmentVariable("USERPROFILE") + "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MonacoPreview-Temp").ConfigureAwait(true).GetAwaiter();
                    webView2EnvironmentAwaiter.OnCompleted(() =>
                    {
                        InvokeOnControlThread(async () =>
                        {
                            webView2Environment = webView2EnvironmentAwaiter.GetResult();
                            var vsCodeLangSet = fileHandler.GetLanguage(Path.GetExtension(filePath).TrimStart('.'));
                            var fileContent = File.ReadAllText(filePath);
                            var base64FileCode =
                                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

                            // prepping index html to load in
                            var html = File.ReadAllText(AssemblyDirectory + "\\index.html").Replace("\t", "");

                            html = html.Replace("[[PT_LANG]]", vsCodeLangSet);
                            html = html.Replace("[[PT_WRAP]]", settings.wrap ? "1" : "0");
                            html = html.Replace("[[PT_THEME]]", settings.GetTheme(ThemeListener.AppMode));
                            html = html.Replace("[[PT_CODE]]", base64FileCode);
                            html = html.Replace("[[PT_URL]]", VirtualHostName);

                            // Initialize WebView

                            await webView.EnsureCoreWebView2Async(webView2Environment);

                            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AssemblyDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
                            webView.NavigateToString(html);
                            webView.NavigationCompleted += WebView2Init;
                            webView.Height = this.Height;
                            webView.Width = this.Width;
                            Controls.Add(webView);
                            
                            base.DoPreview(dataSource);
                        });
                    });
                    
                });
                
                

            }
            catch(Exception e)
            {
                InvokeOnControlThread(() => {
                    Label text = new Label();
                    text.Text = "Exception occured:\n";
                    text.Text += e.Message;
                    text.Text += "\n" + e.Source;
                    text.Text += "\n" + e.StackTrace;
                    text.Width = 500;
                    text.Height = 10000;
                    Controls.Add(text);
                });
            }

            this.Resize += FormResize;

        }
        
        public void FormResize(Object sender, EventArgs e)
        {
            // This function gets called when the form gets resized
            // It's fitting the webview in the size of the window
            webView.Height = this.Height;
            webView.Width = this.Width;
        }
        
        public string[] GetFile(string args)
        {
            // This function gets a file
            string[] returnValue = new string[3];
            // Get source code
            returnValue[0] = File.ReadAllText(args);
            // Gets file extension (without .)
            returnValue[1] = Path.GetExtension(args).Replace(".","");
            returnValue[2] = args;
            return returnValue;
        }
        [STAThread]
        private void WebView2Init(Object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // This function sets the diiferent settings for the webview 

            // Checks if already navigated
            if (!WasNavigated)
            {
                CoreWebView2Settings settings = (sender as WebView2).CoreWebView2.Settings;

                // Disable contextmenu
                //settings.AreDefaultContextMenusEnabled = false;
                // Disable developer menu
#if DEBUG
                settings.AreDevToolsEnabled = true;
#else
                settings.AreDevToolsEnabled = false;
#endif
                // Disable script dialogs (like alert())
                settings.AreDefaultScriptDialogsEnabled = false;
                // Enables JavaScript
                settings.IsScriptEnabled = true;
                // Disable zoom woth ctrl and scroll
                settings.IsZoomControlEnabled = false;
                // Disable developer menu
                settings.IsBuiltInErrorPageEnabled = false;
                // Disable status bar
                settings.IsStatusBarEnabled = false;
                Controls.Remove(_loading);
            }
        }
        [STAThread]
        private void NavigationStarted(Object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Prevents navigation if already one done to index.html
            if (WasNavigated)
            {
                e.Cancel = false;
            }

            // If it has navigated to index.html it stops further navigations
            if(e.Uri.StartsWith(settings.baseURL))
            {
                WasNavigated = true;
            }
        }
        
        public static string AssemblyDirectory
        {
            // Source: https://stackoverflow.com/a/283917/14774889
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        const string VirtualHostName = "PowerToysLocalMonaco";

        private void InitializeLoadingScreen()
        {
            InvokeOnControlThread(() =>
            {
                _loading = new Label();
                _loading.Text = "Loading...";
                _loading.Width = 500;
                _loading.Height = 60;
                _loading.Font = new Font("MS Sans Serif",16,FontStyle.Bold);
                _loading.BackColor = Color.White;
                Controls.Add(_loading);
            });
        }
    }
}
