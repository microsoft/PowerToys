using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Wox.Infrastructure;
using YAMP.Numerics;

namespace Wox.Plugin.SystemPlugins.SuggestionSources
{
    public class Baidu : AbstractSuggestionSource
    {
        Regex reg = new Regex("window.baidu.sug\\((.*)\\)");

        public override List<string> GetSuggestions(string query)
        {
            try
            {
                var response =
                    HttpRequest.CreateGetHttpResponse(
                        "http://suggestion.baidu.com/su?json=1&wd=" + Uri.EscapeUriString(query), null,
                        null, null);
                var stream = response.GetResponseStream();

                if (stream != null)
                {
                    var body = new StreamReader(stream, Encoding.GetEncoding("GB2312")).ReadToEnd();
                    Match m = reg.Match(body);
                    if (m.Success)
                    {
                        var json = JsonConvert.DeserializeObject(m.Groups[1].Value) as JContainer;
                        if (json != null)
                        {
                            var results = json["s"] as JArray;
                            if (results != null)
                            {
                                return results.OfType<JValue>().Select(o => o.Value).OfType<string>().ToList();
                            }
                        }
                    }
                }
            }
            catch
            { }

            return null;
        }
    }
}
