using System;
using System.IO;
using System.Runtime.InteropServices;
using Common;
using PreviewHandlerCommon;


namespace MonacoPreviewHandler
{
    [Guid("afbd5a44-2520-4ae0-9224-6cfce8fe4400")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MonacoPreviewHandler : FileBasedPreviewHandler, IDisposable
    {
        
        private MonacoPreviewHandlerControl _monacoPreviewHandlerControl;
        private bool disposedValue;

        public MonacoPreviewHandler()
        {
            using (StreamWriter writer = new StreamWriter("C:\\Users\\aaron\\Desktop\\testfolder\\111111.txt"))  
            {  
                writer.WriteLine("MonacoPreviewHandlerStart");
            } 
            Initialize();
        }
        
        public override void DoPreview()
        {
            try
            {
                _monacoPreviewHandlerControl.DoPreview(FilePath);
            }
            catch (Exception e)
            {
                using (StreamWriter writer = new StreamWriter("C:\\Users\\aaron\\Desktop\\testfolder\\111111.txt"))  
                {  
                    writer.WriteLine(e.Message);
                } 
            }
        }
        
        protected override IPreviewHandlerControl CreatePreviewHandlerControl()
        {
            _monacoPreviewHandlerControl = new MonacoPreviewHandlerControl();

            return _monacoPreviewHandlerControl;
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _monacoPreviewHandlerControl.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }
        
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}