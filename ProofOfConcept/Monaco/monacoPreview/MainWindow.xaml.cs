using System;
using System.IO;
using System.Web;
using Microsoft.Web.WebView2.Core;
using WK.Libraries.WTL;

namespace monacoPreview
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    { 
        // This variable prevents users from navigating
        private bool WasNavigated = false;

        // Settings from Settings.cs
        private readonly Settings settings = new Settings();

        // Filehandler class from FileHandler.cs
        private readonly FileHandler fileHandler = new FileHandler();

        public MainWindow()
        {
            System.Diagnostics.Debug.WriteLine("Start");

            // Get command line args
            string[] args = Environment.GetCommandLineArgs();

            InitializeComponent();

            string[] file = GetFile(args);

            var fileName = "";
            if(args != null && args.Length > 1)
            {
                fileName = args[1];
            }

            InitializeAsync(fileName);
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
                settings.AreDevToolsEnabled = false;
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

            File.Delete(fullCustomFilePath);
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

        string customFileName = "powerToysPreview" + Guid.NewGuid().ToString("N") + ".html";
        string fullCustomFilePath;
        public async void InitializeAsync(string fileName)
        {
            // This function initializes the webview settings
            // Partely copied from https://weblog.west-wind.com/posts/2021/Jan/14/Taking-the-new-Chromium-WebView2-Control-for-a-Spin-in-NET-Part-1

            var vsCodeLangSet = fileHandler.GetLanguage(Path.GetExtension(fileName).TrimStart('.'));
            var fileContent = File.ReadAllText(fileName);
            var base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

            // prepping index html to load in
            var html = File.ReadAllText("index.html").Replace("\t", "");

            html = html.Replace("[[PT_LANG]]", vsCodeLangSet);
            html = html.Replace("[[PT_WRAP]]", settings.wrap ? "1" : "0");
            html = html.Replace("[[PT_THEME]]", settings.GetTheme(ThemeListener.AppMode));
            html = html.Replace("[[PT_CODE]]", base64FileCode);
            File.WriteAllText(customFileName, html);
            fullCustomFilePath = Path.Combine(AppContext.BaseDirectory, customFileName);
            // Initialize WebView
            //webView.Source = GetURLwithCode(base64FileCode, vsCodeLangSet);
            webView.Source = new Uri(fullCustomFilePath);
            await webView.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync());

            //webView.NavigateToString(html);
            webView.NavigationCompleted += WebView2Init;
            webView.NavigationStarting += NavigationStarted;
        }

        public Uri GetURLwithCode(string code, string lang)
        {
            // This function returns a url you can use to access index.html

            // Converts code to base64
            code = HttpUtility.UrlEncode(code); // this is needed for URL encode;

            return new Uri(settings.baseURL + "?code=" + code + "&lang=" + lang + "&theme=" + settings.GetTheme(ThemeListener.AppMode) + "&wrap=" + (settings.wrap ? "1" : "0"));
        }
    }
} 
