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

public partial class SpongebotPage : MarkdownPage, IFallbackHandler
{
    public CopyTextCommand CopyCommand { get; set; } = new(string.Empty);

    // Name, Icon, IPropertyChanged: all those are defined in the MarkdownPage base class
    public SpongebotPage()
    {
        Name = string.Empty;

        Icon = new("https://imgflip.com/s/meme/Mocking-Spongebob.jpg");
        Commands = [new CommandContextItem(CopyCommand)];
    }

    public void UpdateQuery(string query)
    {
        this.Name = string.IsNullOrEmpty(query) ? string.Empty : ConvertToAlternatingCase(query);

        CopyCommand.Text = this.Name;
    }

    private static string ConvertToAlternatingCase(string input)
    {
        var sb = new StringBuilder();
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
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "state.json");
    }
}
