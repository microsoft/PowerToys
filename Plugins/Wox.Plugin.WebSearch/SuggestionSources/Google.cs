using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class Google : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query)
        {
            string result;
            try
            {
                const string api = "https://www.google.com/complete/search?output=chrome&q=";
                result = await Http.Get(api + Uri.EscapeUriString(query));
            }
            catch (WebException e)
            {
                Log.Exception("|Google.Suggestions|Can't get suggestion from google", e);
                return new List<string>();
                ;
            }
            if (string.IsNullOrEmpty(result)) return new List<string>();
            JContainer json;
            try
            {
                json = JsonConvert.DeserializeObject(result) as JContainer;
            }
            catch (JsonSerializationException e)
            {
                Log.Exception("|Google.Suggestions|can't parse suggestions", e);
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

        public override string ToString()
        {
            return "Google";
        }
    }
}