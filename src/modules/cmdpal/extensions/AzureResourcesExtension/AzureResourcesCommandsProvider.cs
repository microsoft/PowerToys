using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Windows.DevPal.SDK;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace AzureResourcesExtension;

internal sealed class AzureResourceItem
{
    internal string Name { get; init; }
    internal string Type { get; init; }
    internal string ResourceGroup { get; init; }
    internal string Id { get; init; }

    internal string EscapedName => JsonEncodedText.Encode(Name).ToString();
    internal string EscapedType => JsonEncodedText.Encode(Type).ToString();
    internal string EscapedResourceGroup => JsonEncodedText.Encode(ResourceGroup).ToString();
}

internal sealed class AzureResourcesPage : IPage
{
    internal List<AzureResourceItem> resources = new();

    public IAsyncOperation<string> RenderToJson()
    {
        return AsyncInfo.Run(async cancellationToken =>
        {
            try
            {
                if (resources.Count == 0)
                {
                    resources = await GetAzureResources();
                }

                var items = string.Join(", ", resources.Select(
                    (resource, index) => $$"""
                    {
                      "name": "{{resource.EscapedName}}",
                      "subtitle": "{{resource.EscapedType}}",
                      "description": "{{resource.EscapedResourceGroup}}",
                      "actions": [
                      { "icon": "", "name": "Open in Azure Portal", "id": "AzureResource.OpenPortal.{{index}}" }
                    ]
                    }
                    """
                ));

                var json = $$"""
                {
                    "type": "list",
                    "items": [
                        {{items}}
                    ]
                }
                """;

                return json;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RenderToJson: {ex.Message}");
                throw;
            }
        });
    }

    private static async Task<List<AzureResourceItem>> GetAzureResources()
    {
        var resources = new List<AzureResourceItem>();

        try
        {
            var azPath = @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd";

            var processInfo = new ProcessStartInfo
            {
                FileName = azPath,
                Arguments = "resource list -o json",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) // Set a valid working directory
            };

            using var process = Process.Start(processInfo);
            var result = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            var resourceList = JsonSerializer.Deserialize<List<AzureResource>>(result);
            resources = resourceList.Select(resource => new AzureResourceItem
            {
                Name = resource.name,
                Type = resource.type,
                ResourceGroup = resource.resourceGroup,
                Id = resource.id
            }).ToList();
        }
        catch (Exception ex)
        {
            // return a really simple json in the proper format
            return new List<AzureResourceItem>
            {
                new() {
                    Name = ex.Message,
                    Type = "Error",
                    ResourceGroup = ex.Message
                }
            };
        }

        return resources;
    }

    private sealed class AzureResource
    {
        public string name { get; set; }
        public string type { get; set; }
        public string resourceGroup { get; set; }
        public string id { get; set; }
    }
}

internal sealed class AzureResourcesCommand : ICommand
{
    private readonly AzureResourcesPage _page = new();
    public string Icon => "https://www.c-sharpcorner.com/UploadFile/BlogImages/01232023170209PM/Azure%20Icon.png";
    public CommandType Kind => CommandType.List;
    public string Name => "Azure Resources";
    public string Subtitle => "";
    public IPage Page => _page;
#pragma warning disable CS0067
    public event TypedEventHandler<object, NavigateToCommandRequestedEventArgs> NavigateToCommandRequested;
#pragma warning restore
    public IAsyncOperation<IReadOnlyList<ICommand>> GetCommandsForQueryAsync(string search) { return null; }
    public IAsyncAction DoAction(string actionId)
    {
        return Task.Run(() =>
        {
            try
            {
                if (_page.resources.Count != 0)
                {
                    var parts = actionId.Split('.');
                    if (parts.Length != 3)
                    {
                        return;
                    }
                    var index = int.Parse(parts[^1], null);
                    var action = parts[^2];

                    var resource = _page.resources[index];
                    if (action == "OpenPortal")
                    {
                        OpenResourceInAzurePortal(resource.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DoAction: {ex.Message}");
                throw;
            }
        }).AsAsyncAction();
    }

    private static void OpenResourceInAzurePortal(string resourceId)
    {
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        if (string.IsNullOrEmpty(tenantId))
        {
            Debug.WriteLine("Environment variable AZURE_TENANT_ID is not set.");
            return;
        }
        var url = $"https://portal.azure.com/#@{tenantId}/resource{resourceId}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

internal sealed class AzureResourcesCommandsProvider : ICommandsProvider
{
    public string DisplayName => "Azure Resources Commands";

    public string Icon => "";

    public void Dispose() => throw new NotImplementedException();

    public IAsyncOperation<IReadOnlyList<ICommand>> GetCommands()
    {
        var list = new List<ICommand>()
        {
            new AzureResourcesCommand()
        };
        return Task.FromResult(list as IReadOnlyList<ICommand>).AsAsyncOperation();
    }
}
