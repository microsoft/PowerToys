// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// A single validated switch in a generated plan.
    /// </summary>
    /// <param name="Name">The switch including its leading slash, e.g. "/R".</param>
    /// <param name="Value">The switch value without the colon, or an empty string for plain flags.</param>
    public record RobocopyPlanOption(string Name, string Value);
}
