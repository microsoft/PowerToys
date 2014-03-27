using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Wox.Infrastructure;
using YAMP.Numerics;

namespace Wox.Plugin.System.SuggestionSources
{
    public class Google : AbstractSuggestionSource
    {
        public override List<string> GetSuggestions(string query)
        {
            try
            {
                var response =
                    HttpRequest.CreateGetHttpResponse(
                        "https://www.google.com/complete/search?output=chrome&q=" + Uri.EscapeUriString(query), null,
                        null, null);
                var stream = response.GetResponseStream();

                if (stream != null)
                {
                    var body = new StreamReader(stream).ReadToEnd();
                    var json = JsonConvert.DeserializeObject(body) as JContainer;
                    if (json != null)
                    {
                        var results = json[1] as JContainer;
                        if (results != null)
                        {
                            var j = results.OfType<JValue>().Select(o => o.Value);
                            return results.OfType<JValue>().Select(o => o.Value).OfType<string>().ToList();
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
