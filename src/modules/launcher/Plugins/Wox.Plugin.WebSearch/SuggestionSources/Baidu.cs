using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class Baidu : SuggestionSource
    {
        private readonly Regex _reg = new Regex("window.baidu.sug\\((.*)\\)");

        public override async Task<List<string>> Suggestions(string query)
        {
            string result;

            try
            {
                const string api = "http://suggestion.baidu.com/su?json=1&wd=";
                result = await Http.Get(api + Uri.EscapeUriString(query), "GB2312");
            }
            catch (WebException e)
            {
                Log.Exception("|Baidu.Suggestions|Can't get suggestion from baidu", e);
                return new List<string>();
            }

            if (string.IsNullOrEmpty(result)) return new List<string>();
            Match match = _reg.Match(result);
            if (match.Success)
            {
                JContainer json;
                try
                {
                    json = JsonConvert.DeserializeObject(match.Groups[1].Value) as JContainer;
                }
                catch (JsonSerializationException e)
                {
                    Log.Exception("|Baidu.Suggestions|can't parse suggestions", e);
                    return new List<string>();
                }

                if (json != null)
                {
                    var results = json["s"] as JArray;
                    if (results != null)
                    {
                        return results.OfType<JValue>().Select(o => o.Value).OfType<string>().ToList();
                    }
                }
            }

            return new List<string>();
        }

        public override string ToString()
        {
            return "Baidu";
        }
    }
}