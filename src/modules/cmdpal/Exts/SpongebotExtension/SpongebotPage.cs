// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SpongebotExtension;

public class SpongebotPage : MarkdownPage, IFallbackHandler
{
    public CopyTextAction CopyTextAction { get; set; } = new(string.Empty);

    // Name, Icon, IPropertyChanged: all those are defined in the MarkdownPage base class
    public SpongebotPage()
    {
        Name = string.Empty;

        Icon = new("https://imgflip.com/s/meme/Mocking-Spongebob.jpg");
        Commands = [new CommandContextItem(CopyTextAction)];
    }

    public void UpdateQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            this.Name = string.Empty;
        }
        else
        {
            this.Name = ConvertToAlternatingCase(query);
        }

        CopyTextAction.Text = this.Name;
    }

    private static string ConvertToAlternatingCase(string input)
    {
        StringBuilder sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
#pragma warning disable CA1304 // Specify CultureInfo
            sb.Append(i % 2 == 0 ? char.ToUpper(c) : char.ToLower(c));
#pragma warning restore CA1304 // Specify CultureInfo
        }

        return sb.ToString();
    }

    public override string[] Bodies()
    {
        var t = GenerateMeme(this.Name);
        t.ConfigureAwait(false);
        return [t.Result];
    }

    private static async Task<string> GenerateMeme(string text)
    {
        var client = new System.Net.Http.HttpClient();

        var state = File.ReadAllText(StateJsonPath());
        var jsonState = JsonNode.Parse(state);

        var bodyObj = new Dictionary<string, string>
        {
            { "template_id", "102156234" },
            { "username", jsonState["username"]?.ToString() ?? string.Empty },
            { "password", jsonState["password"]?.ToString() ?? string.Empty },
            { "boxes[0][x]", "0" },
            { "boxes[0][y]", "289" },
            { "boxes[0][width]", "502" },
            { "boxes[0][height]", "49" },
            { "boxes[0][text]", text },
        };

        var content = new System.Net.Http.FormUrlEncodedContent(bodyObj);
        var resp = await client.PostAsync("https://api.imgflip.com/caption_image", content);
        var respBody = await resp.Content.ReadAsStringAsync();
        var response = JsonNode.Parse(respBody);

        var url = response["data"]?["url"]?.ToString() ?? string.Empty;

        var body = $$"""
 SpongeBot says:
![{{text}}]({{url}})

[{{text}}]({{url}})
""";
        return body;
    }

    internal static string StateJsonPath()
    {
        // Get the path to our exe
        var path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Get the directory of the exe
        var directory = Path.GetDirectoryName(path) ?? string.Empty;

        // now, the state is just next to the exe
        return Path.Combine(directory, "state.json");
    }
}
