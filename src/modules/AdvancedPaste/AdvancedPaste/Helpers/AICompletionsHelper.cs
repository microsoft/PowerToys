// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using Azure;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Windows.Security.Credentials;

namespace AdvancedPaste.Helpers
{
    public class AICompletionsHelper
    {
        // Return Response and Status code from the request.
        public struct AICompletionsResponse
        {
            public AICompletionsResponse(string response, int apiRequestStatus)
            {
                Response = response;
                ApiRequestStatus = apiRequestStatus;
            }

            public string Response { get; }

            public int ApiRequestStatus { get; }
        }

        private string _openAIKey;

        public bool IsAIEnabled => !string.IsNullOrEmpty(this._openAIKey);

        public AICompletionsHelper()
        {
            this._openAIKey = LoadOpenAIKey();
        }

        public void SetOpenAIKey(string openAIKey)
        {
            this._openAIKey = openAIKey;
        }

        public string GetKey()
        {
            return _openAIKey;
        }

        public static string LoadOpenAIKey()
        {
            PasswordVault vault = new PasswordVault();

            try
            {
                PasswordCredential cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                if (cred is not null)
                {
                    return cred.Password.ToString();
                }
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        public string GetAICompletion(string systemInstructions, string userMessage)
        {
            OpenAIClient azureAIClient = new OpenAIClient(_openAIKey);

            var response = azureAIClient.GetCompletions(
                new CompletionsOptions()
                {
                    DeploymentName = "gpt-3.5-turbo-instruct",
                    Prompts =
                    {
                        systemInstructions + "\n\n" + userMessage,
                    },
                    Temperature = 0.01F,
                    MaxTokens = 2000,
                });

            if (response.Value.Choices[0].FinishReason == "length")
            {
                Console.WriteLine("Cut off due to length constraints");
            }

            return response.Value.Choices[0].Text;
        }

        public AICompletionsResponse AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            string aiResponse = null;
            int apiRequestStatus = (int)HttpStatusCode.OK;
            try
            {
                aiResponse = this.GetAICompletion(systemInstructions, userMessage);
            }
            catch (Azure.RequestFailedException error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = error.Status;
            }
            catch (Exception error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = -1;
            }

            return new AICompletionsResponse(aiResponse, apiRequestStatus);
        }
    }
}
