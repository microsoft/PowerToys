using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wox.Infrastructure.Http;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class Google : AbstractSuggestionSource
    {
        public override List<string> GetSuggestions(string query)
        {
            var result = HttpRequest.Get("https://www.google.com/complete/search?output=chrome&q=" + Uri.EscapeUriString(query), Proxy);
            if (string.IsNullOrEmpty(result)) return new List<string>();

            try
            {
                JContainer json = JsonConvert.DeserializeObject(result) as JContainer;
                if (json != null)
                {
                    var results = json[1] as JContainer;
                    if (results != null)
                    {
                        return results.OfType<JValue>().Select(o => o.Value).OfType<string>().ToList();
                    }
                }
            }
            catch { }

            return new List<string>();
        }

        public Google(IHttpProxy httpProxy) : base(httpProxy)
        {
        }
    }
}
