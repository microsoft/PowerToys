using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Wox.Plugin.Fanyi;

namespace Wox.Plugin.Youdao
{
    public class TranslateResult
    {
        public int errorCode { get; set; }
        public List<string> translation { get; set; }
        public BasicTranslation basic { get; set; }
        public List<WebTranslation> web { get; set; }
    }

    // 有道词典-基本词典
    public class BasicTranslation
    {
        public string phonetic { get; set; }
        public List<string> explains { get; set; }
    }

    public class WebTranslation
    {
        public string key { get; set; }
        public List<string> value { get; set; }
    }

    public class Main : IPlugin
    {
        private string translateURL = "http://fanyi.youdao.com/openapi.do?keyfrom=WoxLauncher&key=1247918016&type=data&doctype=json&version=1.1&q=";

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results.Add(new Result()
                {
                    Title = "Start to translate between Chinese and English",
                    SubTitle = "Powered by youdao api",
                    IcoPath = "Images\\youdao.ico"
                });
                return results;
            }

            HttpWebResponse response = HttpRequest.CreatePostHttpResponse(translateURL + query.GetAllRemainingParameter(), null, null, null, Encoding.UTF8, null);
            Stream s = response.GetResponseStream();
            if (s != null)
            {
                StreamReader reader = new StreamReader(s, Encoding.UTF8);
                string json = reader.ReadToEnd();
                TranslateResult o = JsonConvert.DeserializeObject<TranslateResult>(json);
                if (o.errorCode == 0)
                {
                    if (o.basic != null && o.basic.phonetic != null)
                    {
                        results.Add(new Result()
                            {
                                Title = o.basic.phonetic,
                                SubTitle = string.Join(",", o.basic.explains.ToArray()),
                                IcoPath = "Images\\youdao.ico",
                            });
                    }
                    foreach (string t in o.translation)
                    {
                        results.Add(new Result()
                            {
                                Title = t,
                                IcoPath = "Images\\youdao.ico",
                            });
                    }
                    if (o.web != null)
                    {
                        foreach (WebTranslation t in o.web)
                        {
                            results.Add(new Result()
                                {
                                    Title = t.key,
                                    SubTitle = string.Join(",", t.value.ToArray()),
                                    IcoPath = "Images\\youdao.ico",
                                });
                        }
                    }
                }
                else
                {
                    string error = string.Empty;
                    switch (o.errorCode)
                    {
                        case 20:
                            error = "要翻译的文本过长";
                            break;

                        case 30:
                            error = "无法进行有效的翻译";
                            break;

                        case 40:
                            error = "不支持的语言类型";
                            break;

                        case 50:
                            error = "无效的key";
                            break;
                    }

                    results.Add(new Result()
                    {
                        Title = error,
                        IcoPath = "Images\\youdao.ico",
                    });
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {

        }
    }
}
