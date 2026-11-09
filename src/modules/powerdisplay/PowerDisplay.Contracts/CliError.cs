// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

/// <summary>
/// Structured CLI error returned by validators and commands. Mapped 1:1 to the JSON
/// <c>error</c> envelope. <see cref="ExitCode"/> is derived from <see cref="Code"/> via
/// <see cref="CliExitCodes.ForErrorCode"/>, so the two can never disagree; callers set only
/// <see cref="Code"/>.
/// </summary>
public sealed class CliError
{
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Stable, fine-grained identifier for the localized message + hint template (e.g.
    /// <c>out-of-range</c>, <c>unknown-setting</c>, <c>invalid-integer</c>). Decoupled from
    /// <see cref="Code"/>: <see cref="Code"/> is coarse and drives the exit code, while several
    /// distinct messages can share one <see cref="Code"/> (e.g. many argument errors are all
    /// <c>ARGUMENT_ERROR</c>). The CLI maps this id to a localized template and fills it from the
    /// structured fields below. Never localized. Empty falls back to <see cref="Message"/>.
    /// </summary>
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// Optional English fallback message. The app leaves this empty and sends only <see cref="Code"/>
    /// and <see cref="MessageId"/> plus the structured fields below; the CLI composes the localized,
    /// human-readable message from <see cref="MessageId"/> (see <c>Resources</c>). This is populated
    /// only as a last-resort fallback for a <see cref="MessageId"/> the CLI does not recognize.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Process exit code for this error, derived from <see cref="Code"/>. Serialized for
    /// JSON consumers; recomputed from <see cref="Code"/> on deserialization.</summary>
    public int ExitCode => CliExitCodes.ForErrorCode(Code);

    /// <summary>
    /// Canonical setting name involved in the error (e.g. <c>brightness</c>, <c>color-temperature</c>).
    /// An identifier, never localized; the CLI substitutes it into the localized template for this
    /// <see cref="Code"/>. Null when the error is not setting-specific.
    /// </summary>
    public string? Setting { get; init; }

    /// <summary>
    /// The offending or selector value as the user supplied it (e.g. <c>150</c>, <c>0x99</c>, a monitor
    /// number/id). Data, never localized; the CLI substitutes it into the localized template. Null when
    /// the error carries no such value.
    /// </summary>
    public string? Value { get; init; }

    public string? ExpectedRange { get; init; }

    public IReadOnlyList<CliSupportedValue>? Supported { get; init; }

    /// <summary>
    /// Optional technical diagnostic kept verbatim (e.g. a VESA/VCP capability reason or a driver error
    /// string). Rendered as-is, not localized: it is low-level hardware jargon aimed at technical users.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Optional English fallback hint. Like <see cref="Message"/>, the app normally leaves this empty
    /// and the CLI derives the localized hint from <see cref="MessageId"/>; used only as a fallback for
    /// an unrecognized <see cref="MessageId"/>.
    /// </summary>
    public string? Hint { get; init; }
}
