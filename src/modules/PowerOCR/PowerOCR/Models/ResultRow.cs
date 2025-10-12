// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Models;

public struct ResultRow
{
    public double Height { get; set; }

    public double Top { get; set; }

    public double Bottom { get; set; }

    public int ID { get; set; }
}
