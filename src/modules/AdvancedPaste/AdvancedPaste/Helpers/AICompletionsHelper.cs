// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using AdvancedPaste.Models;
using Azure;
using Azure.AI.OpenAI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Windows.ApplicationModel.DataTransfer;
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

        private AICompletionsResponse TryAICompletion(string systemInstructions, string userMessage)
        {
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

        public AICompletionsResponse AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.
Ensure that you do all that is requested of you in the instructions. If the user has multiple instructions in their prompt be sure that both are all completed.
Your output can include HTML if necessary, but it is not required.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            return TryAICompletion(systemInstructions, userMessage);
        }

        public string AIFormatStringAsHTML(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to reformat their clipboard content as they have requested it.
Ensure that you do all that is requested of you in the instructions. If the user has multiple instructions in their prompt be sure that both are all completed.
Do not use <code> blocks or classes to style the HTML, instead format directly into the HTML with inline styles wherever possible.
Your output needs to be in HTML format.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            return TryAICompletion(systemInstructions, userMessage).Response;
        }

        public string AIGetHTMLOrPlainTextOutput(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with determining the output format for a user's request to reformat the clipboard data.
You can choose between the output of 'HTML' or 'PlainText'. Your answer can only be those two options, do not put any other output.

Use these examples below to inform you.

Example user instructions:
Make this pretty

Example clipboard content:
var x = 5;

Example output:
HTML

Example user instructions:
Change to a pirate speaking in markdown

Example clipboard content:
Hello my good friend.

Example output:
PlainText

Example user instructions:
Show this data as a table.

Example clipboard content:
T-Rex, 5, 10
Velociraptor, 7, 15

Example output:
HTML

Now output the real answer.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            return TryAICompletion(systemInstructions, userMessage).Response;
        }

        public string GetOperationsFromAI(string inputInstructions, bool hasText, bool hasImage, bool hasHtml, bool hasFile, bool hasAudio)
        {
            string availableFormatString = "(string inputInstructions";
            if (hasText)
            {
                availableFormatString += ", string clipboardText";
            }

            if (hasImage)
            {
                availableFormatString += ", Image clipboardImage";
            }

            if (hasHtml)
            {
                availableFormatString += ", HtmlData clipboardHTML";
            }

            if (hasFile)
            {
                availableFormatString += ", File clipboardFile";
            }

            if (hasAudio)
            {
                availableFormatString += ", Audio clipboardAudio";
            }

            availableFormatString += ")";

            string systemInstructions = $@"You are tasked with determining what operations are needed to reformat a user's clipboard data. Use the user's instructions, available functions, and clipboard data content to output the list of operations needed.
You will output youre response as a function in C# ONLY using the functions provided (Do not use any other C# functions other than what is provided below!)

Available functions:
- string ToJSON(string clipboardText)
   - Returns a string formatted into JSON from the clipboard content, only accepts text
   - Only to be used if the user explicitly requests JSON.
- string ToPlainText(string clipboardText)
   - Returns a string with the clipboard content formatted into plain text, only accepts text
- string ToCustomWithAI(string inputInstructions, string clipboardText)
   - Returns a string with the clipboard content formatted according to the input instructions, only accepts text.
   - Use this function to do custom processing of the text if another function above does not meet the requirements. Feel free to modify the user's instructions as needed to input to this function.
- string ToFile(string clipboardText)
   - Returns a string of the filename of the file created from the input clipboard text
- string ToFile(Image clipboardImage)
   - Returns a string of the filename of the file created from the input clipboard image
- string AudioToText(Audio clipboardAudio, int seekSeconds, int maxDurationSeconds)
   - Returns a string with the clipboard audio content formatted into text, only accepts audio
   - seekSeconds is the number of seconds to skip from the start of the audio file
   - maxDurationSeconds is the maximum number of seconds to process from the audio file
   - If seekSeconds and maxDurationSeconds are 0 and 0 the entire file will be processed.

Example available arguments:
(string inputInstructions, Audio clipboardAudio)

Example user instructions:
To text, convert to Python, and highlight syntax with VS Code highlighting

Example output:
public string ReformatClipboard(string inputInstructions, Audio clipboardAudio)
{{
    string audioText = AudioToText(clipboardAudio, 0, 0);
    string customFormattedText = ToCustomWithAI('Convert to Python', imageText);
    string customFormattedText2 = ToCustomWithAI('Highlight syntax with VS Code highlighting', imageText);
    return customFormattedText2;
}}";

            string userMessage = $@"Available arguments:
{availableFormatString}

User instructions:
{inputInstructions}

Output: 
";

            return TryAICompletion(systemInstructions, userMessage).Response;
        }
    }
}
