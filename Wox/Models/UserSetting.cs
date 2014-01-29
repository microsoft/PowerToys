using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Models
{
    public class UserSetting
    {
        public string Theme { get; set; }
        public bool ReplaceWinR { get; set; }
        public List<WebSearch> WebSearches { get; set; }
    }
}
