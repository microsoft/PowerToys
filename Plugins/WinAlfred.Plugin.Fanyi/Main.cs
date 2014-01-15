using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Newtonsoft.Json;

namespace WinAlfred.Plugin.Fanyi
{
    public class TranslateResult
    {
        public string from { get; set; }
        public string to { get; set; }
        public List<SrcDst> trans_result { get; set; }

    }

    public class SrcDst
    {
        public string src { get; set; }
        public string dst { get; set; }
    }

    public class Main : IPlugin
    {
        private string translateURL = "http://openapi.baidu.com/public/2.0/bmt/translate";
        private string baiduKey = "SnPcDY3iH5jDbklRewkG2D2v";

        private static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results.Add(new Result()
                {
                    Title = "Start to translate between Chinese and English",
                    SubTitle = "Powered by baidu api",
                    IcoPath = "Images\\translate.png"
                });
                return results;
            }

            Dictionary<string, string> data = new Dictionary<string, string>();
            data.Add("from", "auto");
            data.Add("to", "auto");
            data.Add("q", query.RawQuery.Substring(3));
            data.Add("client_id", baiduKey);
            HttpWebResponse response = HttpRequest.CreatePostHttpResponse(translateURL, data, null, null, Encoding.UTF8, null);
            Stream s = response.GetResponseStream();
            if (s != null)
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                string json = reader.ReadToEnd();
                TranslateResult o = JsonConvert.DeserializeObject<TranslateResult>(json);
                foreach (SrcDst srcDst in o.trans_result)
                {
                    string dst = srcDst.dst;
                    results.Add(new Result()
                    {
                        Title = dst,
                        SubTitle = "Copy to clipboard",
                        IcoPath = "Images\\translate.png",
                        Action = () =>
                        {
                            Clipboard.SetText(dst);
                            context.ShowMsg("translation has been copyed to your clipboard.", "",
                                AssemblyDirectory + "\\Images\\translate.png");
                        }
                    });
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        private PluginInitContext context { get; set; }
    }
}
