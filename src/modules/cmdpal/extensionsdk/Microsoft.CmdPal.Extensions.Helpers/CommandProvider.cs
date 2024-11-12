// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class CommandProvider : ICommandProvider
{
    private string _displayName = string.Empty;

    private IconDataType _icon = new(string.Empty);

    private ICommandSettings? _settings;

    public string DisplayName { get => _displayName; protected set => _displayName = value; }

    public IconDataType Icon { get => _icon; protected set => _icon = value; }

    public virtual IListItem[] TopLevelCommands() => throw new NotImplementedException();

    public ICommandSettings? Settings { get => _settings; protected set => _settings = value; }

    public void InitializeWithHost(IExtensionHost host)
    {
        ExtensionHost.Initialize(host);
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

}
