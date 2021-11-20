using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.Core;
using WK.Libraries.WTL;

namespace monacoPreview
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    { 
        // This variable prevents users from navigating to other pages in the webView.
        // It's first set to false and as soon as the WebView loaded index.html it's set to true to prevent further navigation.
        private bool _hasNavigated;

        // Settings from Settings.cs
        private readonly Settings _settings = new Settings();

        // Filehandler class from FileHandler.cs
        private readonly FileHandler _fileHandler = new FileHandler();

        private CoreWebView2Environment _webView2Environment;
        
        public MainWindow()
        { 
            Debug.WriteLine("Starting Monaco Preview Standalone App");

            // Get command line args
            string[] args = Environment.GetCommandLineArgs();

            InitializeComponent();
            
            // Get filename for the file to preview
            var fileName = "";
            if (args.Length > 1)
            {
                // Get first command line argument as file name
                fileName = args[1];
            }
            else
            {
                // Exception if no command line argument is passed.
                Console.WriteLine("Please pass a file path as the first argument.");
                throw new Exception("No file specified for previewing.");
            }

            // Check if the file is too big.
            long fileSize = new FileInfo(args[1]).Length;
            
            if (fileSize < _settings.maxFileSize)
            {
                // Initialize WebView2 element with page
                InitializeAsync(fileName);
            }
            else
            {
                Debug.WriteLine("File was too big to be previewed.");
                Debug.WriteLine("File size: " + fileSize + "bytes / Allowed file size: " + _settings.maxFileSize);
                InitializeAsync(false);
            }
        }
        
        // This function sets the different settings for the webview.
        private void WebView2Init(Object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Checks if already navigated
            if (_hasNavigated) return;
            
            // Initialize CoreWebView2Settings object
            CoreWebView2Settings settings = WebView.CoreWebView2.Settings;

            // Disable contextmenu
            settings.AreDefaultContextMenusEnabled = false;
            // Disable developer menu
            settings.AreDevToolsEnabled = false;
            // Disable script dialogs (like alert())
            settings.AreDefaultScriptDialogsEnabled = false;
            // Enables JavaScript
            settings.IsScriptEnabled = true;
            // Disable zoom with ctrl + scroll
            settings.IsZoomControlEnabled = false;
            // Disable developer menu
            settings.IsBuiltInErrorPageEnabled = false;
            // Disable status bar
            settings.IsStatusBarEnabled = false;
        }

        private void NavigationStarted(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Prevents navigation if already one done to index.html
            if (_hasNavigated)
            {
                e.Cancel = false;
            }

            // If it has navigated to index.html it stops further navigations
            if(e.Uri.StartsWith(_settings.BaseUrl))
            {
                _hasNavigated = true;
            }
        }

        private void FormResize(object sender, EventArgs e)
        {
            // This function gets called when the form gets resized
            // It's fitting the webview in the size of the window
            Viewbox.Height = this.ActualHeight - 50;
            Viewbox.Width = this.ActualWidth;
            WebView.Height = this.ActualHeight - 50;
            WebView.Width = this.ActualWidth;
        }

        // Get directory of assembly
        private static string AssemblyDirectory
        {
            // Source: https://stackoverflow.com/a/283917/14774889
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                if (codeBase == null)
                {
                    throw new NoNullAllowedException("Assembly.GetExecutingAssembly().CodeBase gave back null. That's not allowed");
                }
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
        
        // Virtual host name for in index.html
        const string VirtualHostName = "PowerToysLocalMonaco";
        private async void InitializeAsync(string fileName)
        {
            // This function initializes the webview settings
            // Partly copied from https://weblog.west-wind.com/posts/2021/Jan/14/Taking-the-new-Chromium-WebView2-Control-for-a-Spin-in-NET-Part-1

            // Sets the Monaco language code set
            var vsCodeLangSet = _fileHandler.GetLanguage(Path.GetExtension(fileName).TrimStart('.'));
            // The content of the file
            var fileContent = File.ReadAllText(fileName);
            // Turns file content in base64 string
            var base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

            // Prepping index.html to load in
            var html = File.ReadAllText(AssemblyDirectory+"\\index.html").Replace("\t", "");

            // Replace values in index.html
            html = html.Replace("[[PT_LANG]]", vsCodeLangSet);
            html = html.Replace("[[PT_WRAP]]", _settings.wrap ? "1" : "0");
            html = html.Replace("[[PT_THEME]]", _settings.GetTheme(ThemeListener.AppMode));
            html = html.Replace("[[PT_CODE]]", base64FileCode);
            html = html.Replace("[[PT_URL]]", VirtualHostName);

            // Initialize WebView2Environment with userDataFolder set to a temp path
            _webView2Environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(Path.GetTempPath(),"MonacoPreview"));
            
            // Ensures WebView2 is ready
            await WebView.EnsureCoreWebView2Async(_webView2Environment);
            
            // Sets virtual host name
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            
            // Passes index.html file content to the WebView and navigates to it.
            WebView.NavigateToString(html);
            
            // WebView events
            WebView.NavigationCompleted += WebView2Init;
            WebView.NavigationStarting += NavigationStarted;
        }
        
        // Function if the file size is too big
        private async void InitializeAsync(bool status)
        {
            if (!status)
            {
                // Initialize WebView2Environment with userDataFolder set to a temp path
                _webView2Environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(Path.GetTempPath(),"MonacoPreview"));
            
                // Ensures WebView2 is ready
                await WebView.EnsureCoreWebView2Async(_webView2Environment);
            
                // Sets virtual host name
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
                
                // Navigates WebView to the code specified in <ref>_settings.maxFileSizeErr</ref>
                WebView.NavigateToString(_settings.maxFileSizeErr);
            }
            else
            {
                throw new ArgumentException("Status can only be `false`","status");
            }
        }
    }
} 
