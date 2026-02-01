// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Markup;

namespace ShortcutGuide.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public partial class StringResourceExtension : MarkupExtension
    {
        public enum SpecialTreatment
        {
            None,
            FirstCharOnly,
            EverythingExceptFirstChar,
        }

        public string Key { get; set; } = string.Empty;

        public SpecialTreatment Treatment { get; set; } = SpecialTreatment.None;

        protected override object ProvideValue() => this.Treatment switch
        {
            SpecialTreatment.FirstCharOnly => ResourceLoaderInstance.ResourceLoader.GetString(this.Key)[0].ToString(),
            SpecialTreatment.EverythingExceptFirstChar => ResourceLoaderInstance.ResourceLoader.GetString(this.Key)[1..],
            _ => ResourceLoaderInstance.ResourceLoader.GetString(this.Key),
        };
    }
}
