// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Models;

public struct ResultColumn
{
    public double Width { get; set; }

    public double Left { get; set; }

    public double Right { get; set; }

    public int ID { get; set; }
}
