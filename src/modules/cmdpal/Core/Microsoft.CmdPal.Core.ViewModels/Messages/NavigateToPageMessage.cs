// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.ViewModels.Messages;

public record NavigateToPageMessage(PageViewModel Page, bool WithAnimation, CancellationToken CancellationToken);
