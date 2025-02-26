// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Metadata;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

[Deprecated("Use ContentPage instead", DeprecationType.Deprecate, 8)]
public abstract partial class FormPage : Page, IFormPage
{
    public abstract IForm[] Forms();
}
