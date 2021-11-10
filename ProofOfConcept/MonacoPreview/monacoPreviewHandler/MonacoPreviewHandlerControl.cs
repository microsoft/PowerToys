using System;
using System.Drawing;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Common;
using PreviewHandlerCommon;

namespace MonacoPreviewHandler
{
    public class MonacoPreviewHandlerControl : FormHandlerControl
    {
        
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        
        private WebBrowserExt _browser;
        
        public MonacoPreviewHandlerControl()
        {

        }

        public override void DoPreview<T>(T dataSource)
        {
            if (!(dataSource is string filePath))
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandler)} must be a string but was a '{typeof(T)}'");
            }
            
            string fileText = File.ReadAllText(filePath);
            
            
            
            InvokeOnControlThread(() => 
            {
                _browser = new WebBrowserExt
                {
                    DocumentText = fileText,
                    Dock = DockStyle.Fill,
                    IsWebBrowserContextMenuEnabled = false,
                    ScriptErrorsSuppressed = true,
                    ScrollBarsEnabled = true,
                    AllowNavigation = false,
                };

                Controls.Add(_browser);
            });


            base.DoPreview(dataSource);
            }
    }
}
