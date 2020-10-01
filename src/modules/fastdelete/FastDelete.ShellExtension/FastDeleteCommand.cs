using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using ExplorerCommandLib;

namespace FastDelete.ShellExtension
{
    [ComVisible(true), Guid("1e1ecd14-ecd9-4eec-8bd4-02dad39ea829")]
    public sealed class FastDeleteCommand : ExplorerCommandBase
    {
        public override ExplorerCommandFlag Flags => ExplorerCommandFlag.Default;

        public override ExplorerCommandState GetState(IEnumerable<string> selectedFiles)
        {
#if false
            using var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\PowerToys FastDelete");
            object value = key.GetValue("Enabled");
            bool enable = value != null ? ((int)value == 1) : true;
            if (!enable) return ExplorerCommandState.Hidden;
#endif

            try
            {
                if (Directory.Exists(selectedFiles.Single()))
                    return ExplorerCommandState.Enabled;
                else
                    return ExplorerCommandState.Disabled;
            }
            catch
            {
                return ExplorerCommandState.Hidden;
            }
        }

        public override string GetTitle(IEnumerable<string> selectedFiles) => "Fast Delete";
        public override string GetToolTip(IEnumerable<string> selectedFiles) => null;

        public override void Invoke(IEnumerable<string> selectedFiles)
        {
            void ThreadProc()
            {
                FastDeleteForm form = new FastDeleteForm();
                form.DirectoryToDelete = new DirectoryInfo(selectedFiles.Single());
                form.ShowDialog();
            }

            Thread thread = new Thread(ThreadProc);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }
}
