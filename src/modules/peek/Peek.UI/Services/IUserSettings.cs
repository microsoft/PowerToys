// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI
{
    public interface IUserSettings
    {
        public bool CloseAfterLosingFocus { get; }

        public bool ConfirmFileDelete { get; set; }
    }
}
