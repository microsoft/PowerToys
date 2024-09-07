// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed class SampleFormPage : FormPage
{
    private readonly SampleForm sampleForm = new();

    public override IForm[] Forms() => [sampleForm];

    public SampleFormPage()
    {
        Name = "Sample Form";
        Icon = new(string.Empty);
    }
}
