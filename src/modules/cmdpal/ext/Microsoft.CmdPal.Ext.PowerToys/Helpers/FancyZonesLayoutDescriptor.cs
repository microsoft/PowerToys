// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesLayoutDescriptor
{
    public required string Id { get; init; } // "template:<type>" or "custom:<uuid>"

    public required FancyZonesLayoutSource Source { get; init; }

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required FancyZonesAppliedLayout ApplyLayout { get; init; }

    public FancyZonesTemplateLayout? Template { get; init; }

    public FancyZonesCustomLayout? Custom { get; init; }
}
