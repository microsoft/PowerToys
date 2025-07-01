// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

internal partial class PowerToysCommand : IInvokableCommand
{
    public IIconInfo Icon => throw new NotImplementedException();

    public string Id => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged;

    public ICommandResult Invoke(object sender) => throw new NotImplementedException();
}
