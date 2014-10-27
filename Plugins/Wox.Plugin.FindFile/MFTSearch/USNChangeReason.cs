using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Infrastructure.MFTSearch
{
    internal class USNChangeReason
    {
        public static Dictionary<string, UInt32> USN_REASONS = new Dictionary<string, UInt32> { 
			{"USN_REASON_DATA_OVERWRITE",        0x00000001},
			{"USN_REASON_DATA_EXTEND",           0x00000002},
			{"USN_REASON_DATA_TRUNCATION",       0x00000004},
			{"USN_REASON_NAMED_DATA_OVERWRITE",  0x00000010},
			{"USN_REASON_NAMED_DATA_EXTEND",     0x00000020},
			{"USN_REASON_NAMED_DATA_TRUNCATION", 0x00000040},
			{"USN_REASON_FILE_CREATE",           0x00000100},
			{"USN_REASON_FILE_DELETE",           0x00000200},
			{"USN_REASON_EA_CHANGE",             0x00000400},
			{"USN_REASON_SECURITY_CHANGE",       0x00000800},
			{"USN_REASON_RENAME_OLD_NAME",       0x00001000},
			{"USN_REASON_RENAME_NEW_NAME",       0x00002000},
			{"USN_REASON_INDEXABLE_CHANGE",      0x00004000},
			{"USN_REASON_BASIC_INFO_CHANGE",     0x00008000},
			{"USN_REASON_HARD_LINK_CHANGE",      0x00010000},
			{"USN_REASON_COMPRESSION_CHANGE",    0x00020000},
			{"USN_REASON_ENCRYPTION_CHANGE",     0x00040000},
			{"USN_REASON_OBJECT_ID_CHANGE",      0x00080000},
			{"USN_REASON_REPARSE_POINT_CHANGE",  0x00100000},
			{"USN_REASON_STREAM_CHANGE",         0x00200000},
			{"USN_REASON_TRANSACTED_CHANGE",     0x00400000},
			{"USN_REASON_CLOSE",                 0x80000000}
		};

        public static string ReasonPrettyFormat(UInt32 rsn)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            foreach (var rsnPair in USN_REASONS)
            {
                if ((rsnPair.Value & rsn) != 0)
                    sb.Append(rsnPair.Key + " ");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
