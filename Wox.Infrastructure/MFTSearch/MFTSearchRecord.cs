using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.MFTSearch
{
    public class MFTSearchRecord
    {
        private USNRecord usn;

        public MFTSearchRecord(USNRecord usn)
        {
            this.usn = usn;
        }

        public string FullPath
        {
            get { return usn.FullPath; }
        }

        public bool IsFolder
        {
            get { return usn.IsFolder; }
        }
    }
}
