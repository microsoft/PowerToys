// ... (Previous usings)

namespace ColorPicker.Helpers
{
    public static class ClipboardHelper
    {
        private const uint ErrorCodeClipboardCantOpen = 0x800401D0;

        public static void CopyToClipboard(string colorRepresentationToCopy)
        {
            if (!string.IsNullOrEmpty(colorRepresentationToCopy))
            {
                // Register the color in the history manager
                AppStateHandler.AddToHistory(colorRepresentationToCopy);

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Clipboard.SetDataObject(colorRepresentationToCopy, true);
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        // ... (Error handling logic)
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }
        }
    }
}
