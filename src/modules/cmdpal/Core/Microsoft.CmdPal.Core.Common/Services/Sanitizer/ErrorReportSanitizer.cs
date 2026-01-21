// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services.Sanitizer.Abstraction;

namespace Microsoft.CmdPal.Core.Common.Services.Sanitizer;

/// <summary>
/// Specific sanitizer used for error report content. Builds on top of the generic TextSanitizer.
/// </summary>
public sealed class ErrorReportSanitizer
{
    private readonly TextSanitizer _sanitizer = new(BuildProviders(), onGuardrailTriggered: OnGuardrailTriggered);

    private static void OnGuardrailTriggered(GuardrailEventArgs eventArgs)
    {
        var msg = $"Sanitization guardrail triggered for rule '{eventArgs.RuleDescription}': original length={eventArgs.OriginalLength}, result length={eventArgs.ResultLength}, ratio={eventArgs.Ratio:F2}, threshold={eventArgs.Threshold:F2}";
        CoreLogger.LogDebug(msg);
    }

    private static IEnumerable<ISanitizationRuleProvider> BuildProviders()
    {
        // Order matters
        return
        [
            new PiiRuleProvider(),
            new UrlRuleProvider(),
            new NetworkRuleProvider(),
            new TokenRuleProvider(),
            new ConnectionStringRuleProvider(),
            new SecretKeyValueRulesProvider(),
            new EnvironmentPropertiesRuleProvider(),
            new FilenameMaskRuleProvider(),
            new ProfilePathAndUsernameRuleProvider()
        ];
    }

    public string Sanitize(string? input) => _sanitizer.Sanitize(input);

    public string SanitizeException(Exception? exception)
    {
        if (exception is null)
        {
            return string.Empty;
        }

        var fullMessage = GetFullExceptionMessage(exception);
        return Sanitize(fullMessage);
    }

    private static string GetFullExceptionMessage(Exception exception)
    {
        List<string> messages = [];
        var current = exception;
        var depth = 0;

        // Prevent infinite loops on pathological InnerException graphs
        while (current is not null && depth < 10)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                messages.Add($"Stack Trace: {current.StackTrace}");
            }

            current = current.InnerException;
            depth++;
        }

        return string.Join(Environment.NewLine, messages);
    }

    public void AddRule(string pattern, string replacement, string description = "")
        => _sanitizer.AddRule(pattern, replacement, description);

    public void RemoveRule(string description)
        => _sanitizer.RemoveRule(description);

    public IReadOnlyList<SanitizationRule> GetRules() => _sanitizer.GetRules();

    public string TestRule(string input, string ruleDescription)
        => _sanitizer.TestRule(input, ruleDescription);
}
