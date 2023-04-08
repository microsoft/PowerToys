// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Community.PowerToys.Run.Plugin.ChatGPT.Properties;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ChatGPT
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, IDisposable, IDelayedExecutionPlugin
    {
        private PluginInitContext _context;

        private bool _disposed;
        private string _iconPath = string.Empty;
        private string _personalAPIKey;

        public string Name => Resources.plugin_name;

        public string Description => Resources.plugin_description;

        private const string PersonalAPIKey = nameof(PersonalAPIKey);

        public List<Result> Query(Query query, bool isFullQuery)
        {
            // TODO: Have a look at Indexer Plugin to use delayed execution queries
            var results = new List<Result>();

            if (query.Search.EndsWith("?"))
            {
                string searchTerm = query.Search;
                string answer = GetChatGPTAnswer(searchTerm);

                Result result = new()
                {
                    // TODO: Replace the split here to find the previous whitespace and avoid splitting a word
                    Title = answer.Substring(0, 60),
                    SubTitle = answer.Substring(60),
                    QueryTextDisplay = searchTerm,

                    // IcoPath = _iconPath,
                };

                results.Add(result);
            }

            return results;
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(query.Search))
            {
                Result emptyResult = new()
                {
                    Title = "Write your query to chatGPT",
                    SubTitle = "Don't forget to end your query with \"?\"",
                };

                results.Add(emptyResult);
            }
            else if (!query.Search.EndsWith("?"))
            {
                Result missingQuestionMarkResult = new()
                {
                    Title = "End the query with \"?\" to process it",
                };

                results.Add(missingQuestionMarkResult);
            }
            else
            {
                Result loadingResult = new()
                {
                    Title = "Processing your query...",
                };

                results.Add(loadingResult);
            }

            return results;
        }

        public string GetChatGPTAnswer(string query)
        {
            JsonObject data = new JsonObject
                {
                    // TODO: Allow to change the model through a setting?
                    { "model", "gpt-3.5-turbo" },
                    {
                        "messages", new JsonArray
                        {
                            new JsonObject
                            {
                                { "role", "user" },
                                { "content", query },
                            },
                        }
                    },
                };

            string url = "https://api.openai.com/v1/chat/completions";
            string contentType = "application/json";
            string responseContent = string.Empty;

            using (var httpClient = new HttpClient())
            {
                // TODO: Allow to set the custom Auth Key with a setting
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer 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");

                var content = new StringContent(data.ToJsonString(), Encoding.UTF8, contentType);
                var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content,
                });

                try
                {
                    if (response.IsSuccessStatusCode)
                    {
                        JsonDocument doc = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
                        JsonElement root = doc.RootElement;
                        JsonElement messageElement = root.GetProperty("choices")[0].GetProperty("message");
                        responseContent = messageElement.GetProperty("content").GetString();

                        // while (responseContent.StartsWith("/n"))
                        // {
                        //    responseContent = responseContent.Replace("/n", string.Empty);
                        // }

                        // TODO: Two whitespaces appear here (gpt returns /n/n at the start), remove them
                    }
                    else
                    {
                        responseContent = $"Failed: {response.StatusCode} - {response.Content.ReadAsStringAsync().Result}";
                    }
                }
                catch (Exception ex)
                {
                    responseContent += ex.Message;
                }
            }

            if (string.IsNullOrEmpty(responseContent))
            {
                responseContent = "Something happened";
            }

            return responseContent;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>();
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.plugin_description;
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            string personalAPIKey = string.Empty;

            if (settings != null && settings.AdditionalOptions != null)
            {
                personalAPIKey = ((PluginAdditionalStringOption)settings.AdditionalOptions.FirstOrDefault(x => x.Key == PersonalAPIKey))?.Value ?? string.Empty;
            }

            _personalAPIKey = personalAPIKey;
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalStringOption()
            {
                Key = PersonalAPIKey,
                DisplayLabel = Resources.personal_api_key,
                Value = string.Empty,
            },
        };

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }

                _disposed = true;
            }
        }
    }
}
