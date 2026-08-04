// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Markup;

namespace FancyZonesEditor.Helpers
{
    /// <summary>
    /// XAML markup extension that resolves a localized string from the module's
    /// <c>Resources.resw</c>. Replaces the WPF <c>{x:Static props:Resources.Key}</c>
    /// pattern, which WinUI 3 does not support.
    /// </summary>
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public partial class StringResourceExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        protected override object ProvideValue() => ResourceLoaderInstance.ResourceLoader.GetString(Key);
    }
}
