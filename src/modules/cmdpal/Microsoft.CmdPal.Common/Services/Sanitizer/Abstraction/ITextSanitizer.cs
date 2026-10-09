// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Services.Sanitizer.Abstraction;

/// <summary>
/// Defines a service that sanitizes text by applying a set of configurable, regex-based rules.
/// Typical use cases include masking secrets, removing PII, or normalizing logs.
/// </summary>
/// <remarks>
/// - Rules are applied in their registered order; rule ordering may affect the final output.
/// - Each rule should have a unique <c>description</c> that acts as its identifier.
/// </remarks>
/// <seealso cref="SanitizationRule"/>
public interface ITextSanitizer
{
    /// <summary>
    /// Sanitizes the specified input by applying all registered rules in order.
    /// </summary>
    /// <param name="input">The input text to sanitize. Implementations should handle <see langword="null"/> safely.</param>
    /// <returns>The sanitized text after all rules are applied.</returns>
    string Sanitize(string? input);

    /// <summary>
    /// Adds a sanitization rule using a .NET regular expression pattern and a replacement string.
    /// </summary>
    /// <param name="pattern">A .NET regular expression pattern used to match text to sanitize.</param>
    /// <param name="replacement">
    /// The replacement text used by <c>Regex.Replace</c>. Supports standard regex replacement tokens,
    /// including numbered groups (<c>$1</c>) and named groups (<c>${name}</c>).
    /// </param>
    /// <param name="description">
    /// A human-readable, unique identifier for the rule. Used to list, test, and remove the rule.
    /// </param>
    /// <remarks>
    /// Implementations typically validate <paramref name="pattern"/> is a valid regex and may reject duplicate <paramref name="description"/> values.
    /// </remarks>
    void AddRule(string pattern, string replacement, string description = "");

    /// <summary>
    /// Removes a previously added rule identified by its <paramref name="description"/>.
    /// </summary>
    /// <param name="description">The unique description of the rule to remove.</param>
    void RemoveRule(string description);

    /// <summary>
    /// Gets a read-only snapshot of the currently registered sanitization rules in application order.
    /// </summary>
    /// <returns>A read-only list of <see cref="SanitizationRule"/> items.</returns>
    IReadOnlyList<SanitizationRule> GetRules();

    /// <summary>
    /// Tests a single rule, identified by <paramref name="ruleDescription"/>, against the provided <paramref name="input"/>,
    /// without applying other rules.
    /// </summary>
    /// <param name="input">The input text to test.</param>
    /// <param name="ruleDescription">The description (identifier) of the rule to test.</param>
    /// <returns>The result of applying only the specified rule to the input.</returns>
    string TestRule(string input, string ruleDescription);
}
