// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace TopToolbar.Actions
{
    public sealed class ActionResult
    {
        public bool Ok { get; set; }

        public string Message { get; set; } = string.Empty;

        public ActionOutput Output { get; set; } = new ActionOutput();
    }
}
