using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Xaml;
using WK.Libraries.WTL;
using System.Windows.Shell;
using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace monacoPreview
{
    /// <summary>
    /// Interaction logic for PreviewWindow.xaml
    /// </summary>
    public partial class PreviewWindow :  System.Windows.Window, IPreviewHandler, IOleWindow, IObjectWithSite
    { 
        // This variable prevents users from navigating
        private bool WasNavigated = false;

        // Settings from Settings.cs
        private readonly Settings settings = new Settings();

        // Filehandler class from FileHandler.cs
        private readonly FileHandler fileHandler = new FileHandler();

        public PreviewWindow()
        {
            System.Diagnostics.Debug.WriteLine("Start");

            // Get command line args
            string[] args = Environment.GetCommandLineArgs();

            InitializeComponent();

            string[] file = GetFile(args);

            InitializeAsync(settings.compatibility?file[0]:file[2], fileHandler.GetLanguage(file[1]));

            
        }

        public string[] GetFile(string[] args)
        {
            // This function gets a file
            string[] returnValue = new string[3];
            // Get source code
            returnValue[0] = File.ReadAllText(args[1]);
            // Gets file extension (without .)
            returnValue[1] = Path.GetExtension(args[1]).Replace(".","");
            returnValue[2] = args[1];
            return returnValue;
        }

        private void WebView2Init(Object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // This function sets the diiferent settings for the webview 

            // Checks if already navigated
            if (!WasNavigated)
            {
                CoreWebView2Settings settings = webView.CoreWebView2.Settings;

                // Disable contextmenu
                //settings.AreDefaultContextMenusEnabled = false;
                // Disable developer menu
                //settings.AreDevToolsEnabled = false;
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
            }
        }

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

        private void FormResize(object sender, EventArgs e)
        {
            // This function gets called whan the form gets resized
            // It's fitting the webview in the size of the window
            // TO-DO: Javascript resize
            viewbox.Height = this.ActualHeight;
            viewbox.Width = this.ActualWidth;
            webView.Height = this.ActualHeight;
            webView.Width = this.ActualWidth;
        }

        public async void InitializeAsync(string code, string lang)
        {
            // This function initializes the webview settings
            // Partely copied from https://weblog.west-wind.com/posts/2021/Jan/14/Taking-the-new-Chromium-WebView2-Control-for-a-Spin-in-NET-Part-1

            // Sets the url
            webView.Source = settings.compatibility?GetURLwithCode(code, lang):GetURLwithFilePath(code, lang);

            // Initialize WebView

            var webViewOptions = new CoreWebView2EnvironmentOptions
            {
                // Enable CORS for local file access.
                AdditionalBrowserArguments = "--disable-web-security"
            };

            var env = await CoreWebView2Environment.CreateAsync(null, null, webViewOptions);
            await webView.EnsureCoreWebView2Async(env);
            webView.NavigationCompleted += WebView2Init;
            webView.NavigationStarting += NavigationStarted;
        }
        
        public Uri GetURLwithFilePath(string file, string lang)
        {
            // This function returns a url you can use to access index.html

            return new Uri(settings.baseURL + "?file=" + file + "&lang=" + lang + "&theme=" + settings.GetTheme(ThemeListener.AppMode) + "&wrap=" + (this.settings.wrap?"1":"0"));
        }

        public Uri GetURLwithCode(string code, string lang)
        {
            // This function returns a url you can use to access index.html

            // Converts code to base64
            code = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(code)).Replace("+", "%2B");

            return new Uri(settings.baseURL + "?code=" + code + "&lang=" + lang + "&theme=" + settings.GetTheme(ThemeListener.AppMode) + "&wrap=" + (this.settings.wrap ? "1" : "0"));
        }

        public void SetWindow(IntPtr hwnd, ref RECT rect)
        {
            
        }

        public void SetRect(ref RECT rect)
        {
            
        }

        public void DoPreview()
        {
            
        }

        public void Unload()
        {
            
        }

        public void SetFocus()
        {
            
        }

        public void QueryFocus(out IntPtr phwnd)
        {
            phwnd = new IntPtr();
        }

        public uint TranslateAccelerator(ref MSG pmsg)
        {
            return 1;
        }

        public void GetWindow(out IntPtr phwnd)
        {
            phwnd = new IntPtr();
        }

        public void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode)
        {
            throw new NotImplementedException();
        }

        public void SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite)
        {
            throw new NotImplementedException();
        }

        public void GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvSite)
        {
            throw new NotImplementedException();
        }
    }
} 
