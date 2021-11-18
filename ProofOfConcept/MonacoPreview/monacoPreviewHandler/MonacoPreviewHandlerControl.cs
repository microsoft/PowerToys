using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Common;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using monacoPreview;
using PreviewHandlerCommon;
using WK.Libraries.WTL;

namespace MonacoPreviewHandler
{
    public class MonacoPreviewHandlerControl : FormHandlerControl
    {
        
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        
        private CoreWebView2Environment webView2Environment;
        
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        
        // This variable prevents users from navigating
        private bool WasNavigated = false;

        // Settings from Settings.cs
        private readonly Settings settings = new Settings();

        // Filehandler class from FileHandler.cs
        private readonly FileHandler fileHandler = new FileHandler();
        
        public MonacoPreviewHandlerControl()
        {

        }

        public override void DoPreview<T>(T dataSource)
        {
            if (!(dataSource is string filePath))
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandler)} must be a string but was a '{typeof(T)}'");
            }
            
                webView = new Microsoft.Web.WebView2.WinForms.WebView2();


                string[] file = GetFile(filePath);

                InitializeAsync(filePath);

                
            

            base.DoPreview(dataSource);
            
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
        public async void InitializeAsync(string fileName)
        {
            // This function initializes the webview settings
            // Partely copied from https://weblog.west-wind.com/posts/2021/Jan/14/Taking-the-new-Chromium-WebView2-Control-for-a-Spin-in-NET-Part-1

            var vsCodeLangSet = fileHandler.GetLanguage(Path.GetExtension(fileName).TrimStart('.'));
            var fileContent = File.ReadAllText(fileName);
            var base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

            // prepping index html to load in
            var html = File.ReadAllText(AssemblyDirectory+"\\index.html").Replace("\t", "");

            html = html.Replace("[[PT_LANG]]", vsCodeLangSet);
            html = html.Replace("[[PT_WRAP]]", settings.wrap ? "1" : "0");
            html = html.Replace("[[PT_THEME]]", settings.GetTheme(ThemeListener.AppMode));
            html = html.Replace("[[PT_CODE]]", base64FileCode);
            html = html.Replace("[[PT_URL]]", VirtualHostName);

            // Initialize WebView
            webView2Environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(Path.GetTempPath(),"MonacoPreview"));
            
            await webView.EnsureCoreWebView2Async(webView2Environment);
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AppDomain.CurrentDomain.BaseDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
            webView.NavigateToString(html);
            webView.NavigationCompleted += WebView2Init;
            
            Controls.Add(webView);
        }
    }
}
