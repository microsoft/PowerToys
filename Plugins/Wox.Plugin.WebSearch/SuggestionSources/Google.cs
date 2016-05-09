using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class Google : SuggestionSource
    {
        public override string Domain { get; set; } = "www.google.com";
        public override async Task<List<string>> GetSuggestions(string query)
        {
            var result = await HttpRequest.Get("https://www.google.com/complete/search?output=chrome&q=" + Uri.EscapeUriString(query), Proxy);
            if (string.IsNullOrEmpty(result)) return new List<string>();
            JContainer json;
            try
            {
                json = JsonConvert.DeserializeObject(result) as JContainer;
            }
            catch (JsonSerializationException e)
            {
                Log.Error(e);
                return new List<string>();
            }
            if (json != null)
            {
                var results = json[1] as JContainer;
                if (results != null)
                {
                    return results.OfType<JValue>().Select(o => o.Value).OfType<string>().ToList();
                }
            }
            return new List<string>();
        }

        public Google(IHttpProxy httpProxy) : base(httpProxy)
        {
        }
    }
}
