using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace Wox.Plugin.Clipboard
{
    public class Main : IPlugin
    {
        private const int MaxDataCount = 100;
        private readonly KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());
        private PluginInitContext context;
        List<string> dataList = new List<string>();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            List<string> displayData = new List<string>();
            if (query.ActionParameters.Count == 0)
            {
                displayData = dataList;
            }
            else
            {
                displayData = dataList.Where(i => i.ToLower().Contains(query.GetAllRemainingParameter().ToLower()))
                        .ToList();
            }

            results.AddRange(displayData.Select(o => new Result
            {
                Title = o,
                IcoPath = "Images\\clipboard.png",
                Action = c =>
                {
                    if (c.SpecialKeyState.CtrlPressed)
                    {
                        context.ShowCurrentResultItemTooltip(o);
                        return false;
                    }
                    else
                    {
                        System.Windows.Forms.Clipboard.SetText(o);
                        context.HideApp();
                        keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
                        return true;
                    }
                }
            }).Reverse());
            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            ClipboardMonitor.OnClipboardChange += ClipboardMonitor_OnClipboardChange;
            ClipboardMonitor.Start();
        }

        void ClipboardMonitor_OnClipboardChange(ClipboardFormat format, object data)
        {
            if (format == ClipboardFormat.Html ||
                format == ClipboardFormat.SymbolicLink ||
                format == ClipboardFormat.Text ||
                format == ClipboardFormat.UnicodeText)
            {
                if (data != null && !string.IsNullOrEmpty(data.ToString()))
                {
                    if (dataList.Contains(data.ToString()))
                    {
                        dataList.Remove(data.ToString());
                    }
                    dataList.Add(data.ToString());

                    if (dataList.Count > MaxDataCount)
                    {
                        dataList.Remove(dataList.First());
                    }
                }
            }
        }
    }
}
