using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Wox.Infrastructure.Http;
using Wox.Infrastructure.Logger;
using HtmlAgilityPack;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class BingWeb : SuggestionSource
    {
        public override async Task<List<string>> Suggestions(string query, string locale = null)
        {
            string result;
            try
            {
                string mkt = string.IsNullOrEmpty(locale) ? "en-us" : locale;
                query = Uri.EscapeUriString(query);
                string apiUrl = $"https://www.bing.com/AS/Suggestions?mkt={mkt}&cp=1&cvid=23851CF6EA6F463F844F3132EE75343A&addfeaturesnoexpansion=aspartner:osjson,feature.newEdge:1&qry={query}";
                result = await Http.Get(apiUrl);
            }
            catch (WebException e)
            {
                Log.Exception("|Bing.web.Suggestions|Can't get suggestion from bing web", e);
                return new List<string>();
            }
            if (string.IsNullOrEmpty(result)) return new List<string>();

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(result);

            var htmlNodes = htmlDoc.DocumentNode.Descendants("li");
            return htmlNodes.Select(node => node.Attributes["query"].Value).ToList();
        }

        public override string ToString()
        {
            return "Bing";
        }
    }
}
