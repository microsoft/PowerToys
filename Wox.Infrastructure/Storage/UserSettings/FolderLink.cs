using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Wox.Infrastructure.Storage.UserSettings {
	public class FolderLink {
        [JsonProperty]
		public string Path { get; set; }

	    public string Nickname
	    {
	        get { return Path.Split(new char[] { System.IO.Path.DirectorySeparatorChar }, StringSplitOptions.None).Last(); }
	    }
    }
}
