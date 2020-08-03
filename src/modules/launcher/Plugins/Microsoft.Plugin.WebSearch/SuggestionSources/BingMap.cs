using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class BingMap : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query, string locale = null)
        {
            string result;
            try
            {
                // {TODO: this is using appid of maps.bing.com for now, needs to get a dedicate appid.}
                string mkt = string.IsNullOrEmpty(locale) ? "en-us" : locale;
                query = Uri.EscapeUriString(query);
                string apiUrl = $"https://www.bing.com/api/v6/Places/AutoSuggest?appid=D41D8CD98F00B204E9800998ECF8427E1FBE79C2&structuredaddress=true&types=business,address,place&mkt={mkt}&setlang={mkt}&q={query}";
                result = await Http.Get(apiUrl);
            }
            catch (WebException e)
            {
                Log.Exception("|Bing.map.Suggestions|Can't get suggestion from bing web", e);
                return new List<string>();
            }
            if (string.IsNullOrEmpty(result)) return new List<string>();

            List<string> ret = new List<string>();
            try
            {
                dynamic json = JObject.Parse(result);

                var suggs = json.value;
                foreach (var sugg in suggs)
                {
                    string type = sugg._type;
                    if (type.Equals("SearchAction"))
                    {
                        ret.Add(sugg.query);
                    }
                    else if (type.Equals("LocalBusiness"))
                    {
                        string name = sugg.name;
                        string addr = sugg.address.text;
                        ret.Add(name + " " + addr);
                    }
                    else if (type.Equals("PostalAddress"))
                    {
                        string text = sugg.text;
                        ret.Add(text);
                    }
                    else if (type.Equals("Place"))
                    {
                        string name = sugg.name;
                        string address = sugg?.address?.text;
                        if (!string.IsNullOrEmpty(name))
                        {
                            ret.Add(name);
                        }
                        else if (!string.IsNullOrEmpty(address))
                        {
                            ret.Add(address);
                        }
                    }
                }
            }
            catch (JsonSerializationException)
            {
                return new List<string>();
            }

            return ret;
        }

        public override string ToString()
        {
            return "Bing Map";
        }
    }
}
