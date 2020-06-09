using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ExplorerCommandLib;

namespace FastDelete.ShellExtension
{
    [ComVisible(true), Guid("1e1ecd14-ecd9-4eec-8bd4-02dad39ea829")]
    public sealed class FastDeleteCommand : ExplorerCommandBase
    {
        public override ExplorerCommandFlag Flags => ExplorerCommandFlag.Default;

        public override ExplorerCommandState GetState(IEnumerable<string> selectedFiles)
        {
            try
            {
                if (Directory.Exists(selectedFiles.Single()))
                    return ExplorerCommandState.Enabled;
                else
                    return ExplorerCommandState.Hidden;
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
            FastDeleteForm form = new FastDeleteForm();
            form.DirectoryToDelete = new DirectoryInfo(selectedFiles.Single());
            form.ShowDialog();
        }
    }
}
