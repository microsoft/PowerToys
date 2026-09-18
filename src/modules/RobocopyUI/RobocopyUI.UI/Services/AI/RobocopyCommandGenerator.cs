// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Turns a natural language description of a copy task into a validated robocopy command.
    /// </summary>
    public sealed class RobocopyCommandGenerator
    {
        private const int MaxRepairAttempts = 2;

        private readonly IAIChatProvider _provider;
        private readonly IReadOnlyList<RobocopyOptionDescriptor> _catalog;
        private readonly Dictionary<string, List<RobocopyOptionDescriptor>> _catalogByName;

        /// <summary>
        /// The user-authored text of the request currently being generated, used to validate that the
        /// model's paths and follow-up questions are grounded in what the user actually asked.
        /// </summary>
        private string _lastRequestText = string.Empty;

        public RobocopyCommandGenerator(IAIChatProvider provider, IReadOnlyList<RobocopyOptionDescriptor> catalog)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            _catalogByName = _catalog
                .GroupBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            _retriever = new SwitchRetriever(_catalog, CommonIntents);
        }

        private readonly SwitchRetriever _retriever;

        /// <summary>
        /// Generates a plan from the running conversation.
        /// </summary>
        /// <param name="turns">The conversation so far, starting with the user's initial description.</param>
        /// <param name="currentSource">The source path currently entered in the UI, if any.</param>
        /// <param name="currentDestination">The destination path currently entered in the UI, if any.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A validated plan, or a plan requesting a follow-up answer.</returns>
        public async Task<RobocopyPlan> GenerateAsync(IReadOnlyList<AIChatTurn> turns, string currentSource, string currentDestination, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turns);

            var conversation = turns.ToList();
            var requestText = string.Join(" ", turns.Where(turn => turn.IsUser).Select(turn => turn.Text));
            var systemPrompt = BuildSystemPrompt(currentSource, currentDestination, requestText);

            // Paths already sitting in the UI count as supplied by the user for validation purposes.
            _lastRequestText = string.Join(" ", new[] { requestText, currentSource, currentDestination }.Where(text => !string.IsNullOrWhiteSpace(text)));

            for (var attempt = 0; ; attempt++)
            {
                var raw = await _provider.CompleteAsync(systemPrompt, conversation, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new AIGenerationException("The AI model returned an empty response. Try rephrasing your request.");
                }

                if (TryParse(raw, out var plan, out var validationError)
                    && !HasHallucinatedPath(plan!, requestText, currentSource, currentDestination, out validationError))
                {
                    // The extra review pass costs a full generation, which on a small local model can
                    // double an already slow request. Only pay for it when the plan can destroy data.
                    if (NeedsReview(plan!))
                    {
                        var refined = await TryRefinePlanAsync(conversation, raw, plan!, requestText, currentSource, currentDestination, cancellationToken).ConfigureAwait(false);
                        return refined ?? plan!;
                    }

                    return plan!;
                }

                if (attempt >= MaxRepairAttempts)
                {
                    Logger.LogWarning($"Robocopy command generation failed validation: {validationError}");

                    // When the request never named the paths, the model was always going to have to
                    // invent them. Asking the user is far more useful than reporting a syntax failure.
                    if (!LooksLikePathPresent(requestText) && string.IsNullOrWhiteSpace(currentSource) && string.IsNullOrWhiteSpace(currentDestination))
                    {
                        return new RobocopyPlan
                        {
                            NeedsFollowUp = true,
                            FollowUpQuestion = "Which folder should I copy from, and where should it go?",
                        };
                    }

                    throw new AIGenerationException("The AI model produced a command that isn't valid robocopy syntax. Try rephrasing your request.");
                }

                // Give the model a chance to correct itself using the specific validation failure.
                // The correction is phrased as a fresh instruction because small models tend to
                // repeat their previous answer when it is echoed back verbatim.
                conversation.Add(new AIChatTurn(false, raw));
                conversation.Add(new AIChatTurn(true, $"That reply was rejected because {validationError}. Reply again with ONLY a single JSON object starting with {{ and ending with }}, following the schema and using only switches from the list. If a switch's letters are the problem, prefer a simpler switch that needs no letters."));
            }
        }

        /// <summary>
        /// Only plans that can delete or overwrite data at the destination are worth a second
        /// generation to review.
        /// </summary>
        private static bool NeedsReview(RobocopyPlan plan) =>
            !plan.NeedsFollowUp
            && plan.Options.Any(option => DestructiveSwitches.Contains(option.Name));

        private static readonly HashSet<string> DestructiveSwitches =
            new(StringComparer.OrdinalIgnoreCase) { "/MIR", "/PURGE", "/MOVE", "/MOV" };

        private static bool IntroducesAdditionalDestructiveSwitches(RobocopyPlan originalPlan, RobocopyPlan refinedPlan)
        {
            if (originalPlan.NeedsFollowUp || refinedPlan.NeedsFollowUp)
            {
                return false;
            }

            var originalDestructive = originalPlan.Options
                .Select(option => option.Name)
                .Where(name => DestructiveSwitches.Contains(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return refinedPlan.Options
                .Select(option => option.Name)
                .Where(name => DestructiveSwitches.Contains(name))
                .Any(name => !originalDestructive.Contains(name));
        }

        /// <summary>
        /// Rejects a plan whose paths the user never supplied.
        /// </summary>
        /// <remarks>
        /// Small models frequently copy the paths straight out of the prompt's worked examples, producing
        /// a confident, well-formed command that points at folders the user never mentioned. That is far
        /// worse than a visible failure, because the command looks plausible and the user may run it.
        /// Prompt wording alone does not reliably stop this, so the paths are verified against the
        /// request text. Paths already entered in the UI are trusted, and a path is accepted if the
        /// request contains it - matching on the leaf segment too, since models legitimately normalize
        /// things like a trailing slash.
        /// </remarks>
        private static bool HasHallucinatedPath(RobocopyPlan plan, string requestText, string currentSource, string currentDestination, out string validationError)
        {
            validationError = string.Empty;

            if (plan.NeedsFollowUp)
            {
                return false;
            }

            if (!IsPathGrounded(plan.Source, requestText, currentSource))
            {
                validationError = $"\"source\" was {plan.Source}, which does not appear in the request. Use the exact source path the user wrote";
                return true;
            }

            if (!IsPathGrounded(plan.Destination, requestText, currentDestination))
            {
                validationError = $"\"destination\" was {plan.Destination}, which does not appear in the request. Use the exact destination path the user wrote";
                return true;
            }

            return false;
        }

        private static bool IsPathGrounded(string path, string requestText, string uiPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(uiPath) && string.Equals(path.Trim(), uiPath.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var trimmed = path.Trim().TrimEnd('\\', '/');

            if (requestText.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Fall back to the leaf folder name so that a reworded but genuine path still passes.
            var leaf = trimmed.Split('\\', '/').LastOrDefault();
            return !string.IsNullOrWhiteSpace(leaf)
                   && leaf.Length > 2
                   && requestText.Contains(leaf, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gives a generated plan one final semantic review pass. Small local models sometimes produce a
        /// structurally valid JSON blob that is still too broad or too destructive for the request. If the
        /// review pass determines the plan is off, the model is asked to produce a corrected JSON object.
        /// </summary>
        private async Task<RobocopyPlan?> TryRefinePlanAsync(IReadOnlyList<AIChatTurn> conversation, string raw, RobocopyPlan plan, string requestText, string currentSource, string currentDestination, CancellationToken cancellationToken)
        {
            var reviewPrompt = "You are reviewing a robocopy JSON command plan. Compare it to the user's request. If the plan matches the request, return the exact same JSON object unchanged. If it does not match or is more destructive than requested, correct it and return a new JSON object with the same schema. Keep source and destination faithful to the request, keep the command minimal, and use only switches from the list. Return only a single JSON object.";
            var reviewConversation = conversation.ToList();
            reviewConversation.Add(new AIChatTurn(false, raw));
            reviewConversation.Add(new AIChatTurn(true, reviewPrompt));

            var reviewed = await _provider.CompleteAsync(reviewPrompt, reviewConversation, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(reviewed))
            {
                return plan;
            }

            if (TryParse(reviewed, out var refinedPlan, out _)
                && !HasHallucinatedPath(refinedPlan!, requestText, currentSource, currentDestination, out _)
                && !IntroducesAdditionalDestructiveSwitches(plan, refinedPlan!))
            {
                return refinedPlan;
            }

            return plan;
        }

        /// <summary>
        /// Builds the system prompt, reusing the cached catalog section.
        /// </summary>
        /// <remarks>
        /// Everything except the trailing UI path hints is identical for every request, and the catalog
        /// section alone is several thousand characters. Rebuilding it on each generation - including
        /// each repair attempt - was pure overhead, so it is composed once per generator instance.
        /// </remarks>
        internal string BuildSystemPrompt(string currentSource, string currentDestination, string requestText = "")
        {
            _cachedPromptPrefix ??= BuildPromptPrefix();

            var builder = new StringBuilder(_cachedPromptPrefix);
            AppendSwitchList(builder, requestText);

            if (string.IsNullOrWhiteSpace(currentSource) && string.IsNullOrWhiteSpace(currentDestination))
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine("The user already entered these paths in the UI; reuse them unless the request says otherwise:");

            if (!string.IsNullOrWhiteSpace(currentSource))
            {
                builder.Append("- source: ").AppendLine(currentSource);
            }

            if (!string.IsNullOrWhiteSpace(currentDestination))
            {
                builder.Append("- destination: ").AppendLine(currentDestination);
            }

            return builder.ToString();
        }

        private string? _cachedPromptPrefix;

        private string BuildPromptPrefix()
        {
            var builder = new StringBuilder();

            builder.AppendLine("""
                You translate a user's plain-language description of a file copy task into a Windows robocopy command.

                Reply with a single JSON object and nothing else. No prose, no markdown, no code fences.
                Start your reply with { and end it with }.

                Normal reply shape (the angle brackets are placeholders, never emit them literally):
                {"needsFollowUp": false, "source": "<source path from the request>", "destination": "<destination path from the request>", "options": [], "explanation": "<what your switches do>", "warnings": []}

                Follow-up reply shape, used ONLY when the source or destination path is genuinely unknown:
                {"needsFollowUp": true, "followUpQuestion": "Which folder should I copy from?"}

                How to decide:
                1. Find the source and destination paths in the request. If either is missing and cannot be
                   guessed, ask a follow-up naming exactly which path you need. Otherwise never ask.
                2. Re-read the request and list every behavior the user asked for.
                3. For each behavior, pick the one switch from the list below that provides it. If no listed
                   switch provides it, leave it out and say so in "explanation".
                4. Build the JSON. Only describe in "explanation" what your switches actually do.

                Never ask a follow-up about speed, timing, duration, logging preferences, or what to do
                "after" copying. Those are either switches or not needed. If both paths are present, set
                needsFollowUp to false and produce a command.

                Rules:
                - "source" and "destination" are the paths from the user's request, copied character for
                  character. If your reply contains a path the user never typed, it is wrong.
                - Every switch must be traceable to something the user actually asked for. Emit nothing
                  extra: a plain copy request gets no switches at all. In particular do not add /E, /R,
                  /W, /MIR, /PURGE, /MOVE, /MT, /COPYALL, /B, /Z, /LOG, /L, /MAX, /MIN, /MAXAGE, /XD,
                  /XF or /COPY unless the user asked for that behavior.
                - Use only switches from the list below. Never invent switches, and never use a real
                  robocopy switch that is absent from the list - this UI cannot apply it.
                - Copy the exact form shown. Text before the colon is "name" (with its leading slash);
                  text after the colon is "value". A switch shown without a colon takes "value": "".
                  Never put a value or a colon inside "name". Never repeat a switch.
                - Do not put "robocopy", the source, or the destination in "options".
                - "warnings" is only for switches that delete or overwrite data, such as /MIR, /PURGE,
                  /MOVE and /MOV. Otherwise use an empty array, and never put general advice in it.
                - To keep attributes, permissions, security, ownership or auditing information, use
                  /COPYALL. Use /COPY only when the user names exact properties, and only with the
                  letters listed for it. Never invent a letter.

                Easily confused switches. Read this before choosing an exclusion switch:
                - /X on its own only REPORTS extra files. It never excludes anything.
                - Skip files OLDER than the destination -> /XO. NEWER than the destination -> /XN.
                  These are opposites; do not substitute one for the other.
                - "extra" files exist only in the DESTINATION -> /XX. "lonely" files exist only in the
                  SOURCE -> /XL.
                - Exclude by file NAME or wildcard -> /XF. By DIRECTORY name -> /XD. By ATTRIBUTE
                  (hidden, system, read only) -> /XA; include only files with an attribute -> /IA.
                - "older than N days" -> /MAXAGE. "newer than N days" -> /MINAGE.
                - Sizes for /MAX and /MIN are in bytes and take digits only.
                - /SECFIX and /TIMFIX only repair files that were SKIPPED. They never make the copy
                  itself include security or timestamps. To copy permissions use /SEC or /COPYALL; to
                  copy directory timestamps use /DCOPY:T.
                - Recursion is off by default. "no subfolders", "top level only" or "just the files in
                  this folder" means emit NEITHER /E nor /S, not some other switch.
                - Subfolders: /E includes EMPTY ones, /S skips them. "include empty", "all subfolders"
                  or plain "recurse" -> /E. Only "skip/exclude empty folders" -> /S.
                - /S means "copy subdirectories". It has nothing to do with security. Security,
                  permissions and ACLs are /SEC; ownership and auditing as well require /COPYALL.

                Worked examples, showing the JSON shape only. Their paths, switches and explanation text
                belong to these made-up requests - never copy any of it into your reply. If a switch
                below is not justified by the user's own words, leave it out.

                Request: Copy C:\Work to D:\WorkBackup including subfolders, retry 5 times, wait 10 seconds between retries
                Reply: {"needsFollowUp": false, "source": "C:\\Work", "destination": "D:\\WorkBackup", "options": [{"name": "/E", "value": ""}, {"name": "/R", "value": "5"}, {"name": "/W", "value": "10"}], "explanation": "Copies all subfolders including empty ones, retrying each failed file 5 times with a 10 second wait.", "warnings": []}

                Request: Copy E:\Share to F:\Share keeping permissions, ownership and auditing information
                Reply: {"needsFollowUp": false, "source": "E:\\Share", "destination": "F:\\Share", "options": [{"name": "/COPYALL", "value": ""}], "explanation": "Copies every file property, including security, ownership and auditing information.", "warnings": []}

                Request: Mirror C:\Site to D:\SiteBackup
                Reply: {"needsFollowUp": false, "source": "C:\\Site", "destination": "D:\\SiteBackup", "options": [{"name": "/MIR", "value": ""}], "explanation": "Mirrors the source to the destination so both match exactly.", "warnings": ["/MIR deletes files at the destination that no longer exist in the source."]}

                Request: Copy C:\In to D:\Out but skip temp and log files
                Reply: {"needsFollowUp": false, "source": "C:\\In", "destination": "D:\\Out", "options": [{"name": "/E", "value": ""}, {"name": "/XF", "value": "*.tmp *.log"}], "explanation": "Copies all subfolders while excluding files matching *.tmp and *.log.", "warnings": []}

                Request: Copy G:\Reports to H:\Reports
                Reply: {"needsFollowUp": false, "source": "G:\\Reports", "destination": "H:\\Reports", "options": [], "explanation": "Copies the files in the source folder to the destination.", "warnings": []}

                Request: Back up my stuff
                Reply: {"needsFollowUp": true, "followUpQuestion": "Which folder should I copy from, and where should it go?"}
                """);

            return builder.ToString();
        }

        /// <summary>
        /// Emits the cheat sheet and switch list, narrowed to the switches relevant to this request.
        /// </summary>
        /// <remarks>
        /// The full list is roughly half the prompt. Retrieving only the plausible switches cuts that
        /// sharply, which matters twice over: ultra-small models have small context windows, and they
        /// choose more accurately from a shorter candidate list because near-identical entries stop
        /// competing. The validator still accepts the whole catalog, so a retrieval miss costs a repair
        /// attempt rather than producing a wrong command.
        /// </remarks>
        private void AppendSwitchList(StringBuilder builder, string requestText)
        {
            var relevant = _retriever.Retrieve(requestText);

            builder.AppendLine();
            AppendCommonSwitches(builder, relevant);

            // The cheat sheet above already carries the descriptions for the switches users actually
            // ask for, so this list only needs names and exact value shapes. Descriptions are kept
            // only for switches the cheat sheet omits.
            builder.AppendLine("Complete list of switches you may use. Each entry shows the exact form to emit.");
            builder.AppendLine("The part before the colon goes in \"name\"; the part after goes in \"value\".");
            builder.AppendLine();

            var describedByCheatSheet = CommonIntents
                .Select(intent => intent.Switch)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var group in _catalog.Where(option => relevant.Contains(option.Name)).GroupBy(option => option.Kind))
            {
                builder.Append("# ").Append(KindHeading(group.Key)).AppendLine();

                foreach (var option in group)
                {
                    builder.Append("- ").Append(UsageForm(option));

                    if (!describedByCheatSheet.Contains(option.Name) && !string.IsNullOrWhiteSpace(option.Description))
                    {
                        builder.Append(" - ").Append(option.Description.TrimEnd(':', ' '));
                    }

                    if (option.Kind == RobocopyOptionKind.MultiSelect && option.AllowedValues.Count > 0)
                    {
                        builder.Append(" Letters: ");
                        builder.Append(string.Join(
                            ", ",
                            option.AllowedValues.Select(value => string.IsNullOrWhiteSpace(value.Description)
                                ? value.Letter
                                : $"{value.Letter}={value.Description}")));
                        builder.Append('.');
                    }

                    builder.AppendLine();
                }

                builder.AppendLine();
            }
        }

        /// <summary>
        /// Maps the intents users actually express
        /// models struggle to pick the right switch out of the full ~90 entry list, so the ones that
        /// cover the overwhelming majority of real requests are surfaced first as a cheat sheet.
        /// </summary>
        /// <remarks>
        /// Wording is taken from the official robocopy documentation but phrased the way a user would
        /// describe the goal, because the model matches on the intent text rather than the switch name.
        /// The near-identical exclusion switches (/XC, /XN, /XO, /XX, /XL) are spelled out individually;
        /// without that the model collapses them onto whichever one it saw first.
        /// </remarks>
        private static readonly (string Intent, string Switch)[] CommonIntents =
        [
            ("copy subfolders, including empty ones", "/E"),
            ("copy subfolders, but skip empty ones", "/S"),
            ("copy only the top N levels of the tree", "/LEV"),
            ("make destination exactly match source (deletes extras)", "/MIR"),
            ("delete destination files that no longer exist in source", "/PURGE"),
            ("move files and directories, deleting them from the source", "/MOVE"),
            ("move only files, deleting them from the source", "/MOV"),
            ("number of retries on failed copies", "/R"),
            ("seconds to wait between retries", "/W"),
            ("wait for share names to be defined", "/TBD"),
            ("pause when the destination runs low on free space", "/LFSM"),
            ("use multiple threads to copy faster", "/MT"),

            // Exclusion family. These read almost identically, so each gets its own line.
            ("exclude files matching a name or wildcard", "/XF"),
            ("exclude directories matching a name or wildcard", "/XD"),
            ("skip source files OLDER than the destination copy", "/XO"),
            ("skip source files NEWER than the destination copy", "/XN"),
            ("skip changed files: same timestamp but different size", "/XC"),
            ("skip EXTRA files that exist only in the destination", "/XX"),
            ("skip LONELY files that exist only in the source, adding nothing new", "/XL"),
            ("exclude files having any of these attributes", "/XA"),
            ("include only files having any of these attributes", "/IA"),
            ("exclude junction points", "/XJ"),

            // Size and age filters.
            ("skip files larger than a size in bytes", "/MAX"),
            ("skip files smaller than a size in bytes", "/MIN"),
            ("skip files older than N days", "/MAXAGE"),
            ("skip files newer than N days", "/MINAGE"),
            ("skip files unused since a date or N days", "/MAXLAD"),
            ("skip files used since a date or N days", "/MINLAD"),
            ("copy only files with the archive attribute set", "/A"),
            ("copy files with the archive attribute and then clear it", "/M"),

            // Copy flags.
            ("choose exactly which file properties to copy", "/COPY"),
            ("copy attributes, permissions, ownership or auditing info", "/COPYALL"),
            ("copy everything including security, owner and auditing", "/COPYALL"),
            ("copy permissions, ACLs or NTFS security along with the files", "/SEC"),
            ("choose which DIRECTORY properties to copy, such as directory timestamps", "/DCOPY"),
            ("copy no file info at all, useful together with /PURGE", "/NOCOPY"),
            ("repair security on files that were SKIPPED and not copied", "/SECFIX"),
            ("repair timestamps on FILES that were SKIPPED and not copied", "/TIMFIX"),
            ("create the directory tree and zero-length files only", "/CREATE"),

            // Transfer mode and performance.
            ("restartable mode for large or unreliable copies", "/Z"),
            ("backup mode, to read files you would otherwise be denied", "/B"),
            ("restartable mode that falls back to backup mode on access denied", "/ZB"),
            ("unbuffered I/O, recommended for very large files", "/J"),
            ("request network compression during transfer", "/COMPRESS"),
            ("free bandwidth on slow links with an inter-packet gap", "/IPG"),
            ("copy encrypted files in EFS RAW mode", "/EFSRAW"),
            ("assume FAT file times with two-second precision", "/FFT"),
            ("compensate for one-hour daylight saving differences", "/DST"),
            ("turn off support for paths longer than 256 characters", "/256"),

            // Logging.
            ("test run that reports what would happen without copying", "/L"),
            ("write output to a log file, overwriting it", "/LOG"),
            ("write output to a log file, appending to it", "/LOG+"),
            ("write output to both the console and a log file", "/TEE"),
            ("verbose output that also shows skipped files", "/V"),
            ("hide the copy progress percentage", "/NP"),
            ("show the estimated time of arrival", "/ETA"),
            ("include full path names in the output", "/FP"),

            // Scheduling.
            ("only run during certain hours", "/RH"),
            ("run again when more than N changes are detected", "/MON"),
            ("run again after N minutes if changes are detected", "/MOT"),
        ];

        /// <summary>
        /// Emits the cheat sheet, limited to switches this UI actually offers.
        /// </summary>
        private void AppendCommonSwitches(StringBuilder builder, HashSet<string> relevant)
        {
            var available = CommonIntents
                .Where(intent => _catalogByName.ContainsKey(intent.Switch) && relevant.Contains(intent.Switch))
                .ToList();

            if (available.Count == 0)
            {
                return;
            }

            builder.AppendLine("Most requests are covered by these. Match the user's intent to one of these first:");

            foreach (var (intent, switchName) in available)
            {
                var descriptor = _catalogByName[switchName]
                    .OrderBy(candidate => candidate.Kind == RobocopyOptionKind.Flag ? 0 : 1)
                    .First();

                // Prefer the valued variant when the switch has one, since these intents describe values.
                var preferred = _catalogByName[switchName].Count > 1
                    ? _catalogByName[switchName].First(candidate => candidate.Kind != RobocopyOptionKind.Flag)
                    : descriptor;

                builder.Append("- ").Append(intent).Append(" -> ").Append(UsageForm(preferred)).AppendLine();
            }

            builder.AppendLine();
        }

        private static string KindHeading(RobocopyOptionKind kind) => kind switch
        {
            RobocopyOptionKind.Flag => "Switches that take no value (always use \"value\": \"\")",
            RobocopyOptionKind.Number => "Switches that take a whole number",
            RobocopyOptionKind.Storage => "Switches that take a size: digits followed by K, M or G",
            RobocopyOptionKind.Text => "Switches that take text",
            RobocopyOptionKind.MultiSelect => "Switches that take one or more letters joined together",
            RobocopyOptionKind.RunHours => "Switches that take a time window as hhmm-hhmm",
            _ => "Switches",
        };

        /// <summary>
        /// Renders the exact command-line form of a switch, so the model can copy the shape rather
        /// than guess at it.
        /// </summary>
        private static string UsageForm(RobocopyOptionDescriptor option) => option.Kind switch
        {
            RobocopyOptionKind.Flag => option.Name,
            RobocopyOptionKind.Number => $"{option.Name}:n",
            RobocopyOptionKind.Storage => $"{option.Name}:n[K|M|G]",
            RobocopyOptionKind.Text => $"{option.Name}:text",
            RobocopyOptionKind.MultiSelect => $"{option.Name}:{string.Concat(option.AllowedValueLetters)}",
            RobocopyOptionKind.RunHours => $"{option.Name}:hhmm-hhmm",
            _ => option.Name,
        };

        /// <summary>
        /// Finds supported switches whose names are close to an unsupported one, so the repair
        /// message can point the model at a real alternative instead of just rejecting it.
        /// </summary>
        private string SuggestAlternatives(string name)
        {
            var bare = name.TrimStart('/');

            var matches = _catalogByName.Keys
                .Where(candidate =>
                {
                    var candidateBare = candidate.TrimStart('/');
                    return candidateBare.StartsWith(bare, StringComparison.OrdinalIgnoreCase)
                           || bare.StartsWith(candidateBare, StringComparison.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            return matches.Count > 0 ? string.Join(", ", matches) : "listed above";
        }

        /// <summary>
        /// Reports whether the request text already names at least two locations, meaning a follow-up
        /// asking for the source or destination would just be asking the user to repeat themselves.
        /// </summary>
        private static bool LooksLikePathPresent(string requestText)
        {
            if (string.IsNullOrWhiteSpace(requestText))
            {
                return false;
            }

            var matches = System.Text.RegularExpressions.Regex.Matches(
                requestText,
                @"(?:[A-Za-z]:\\|\\\\)[^\s""']*");

            return matches.Count >= 2;
        }

        private bool TryParse(string raw, out RobocopyPlan? plan, out string validationError)
        {
            plan = null;
            validationError = string.Empty;

            var json = ExtractJson(raw);
            if (json is null)
            {
                validationError = "the response was not a JSON object";
                return false;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                validationError = "the response was not valid JSON";
                return false;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    validationError = "the response was not a JSON object";
                    return false;
                }

                if (root.TryGetProperty("needsFollowUp", out var followUpElement)
                    && followUpElement.ValueKind == JsonValueKind.True)
                {
                    var question = GetString(root, "followUpQuestion");
                    if (string.IsNullOrWhiteSpace(question))
                    {
                        validationError = "needsFollowUp was true but followUpQuestion was missing";
                        return false;
                    }

                    // A follow-up is only legitimate when a path really is missing. Small models
                    // otherwise fall back to the example follow-up even when both paths were given,
                    // which strands the user in a loop re-answering what they already said.
                    if (LooksLikePathPresent(_lastRequestText))
                    {
                        validationError = "the request already contains both paths, so no follow-up question is needed";
                        return false;
                    }

                    plan = new RobocopyPlan { NeedsFollowUp = true, FollowUpQuestion = question };
                    return true;
                }

                var source = GetString(root, "source");
                var destination = GetString(root, "destination");

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
                {
                    validationError = "source and destination are both required";
                    return false;
                }

                if (!TryParseOptions(root, out var options, out validationError))
                {
                    return false;
                }

                plan = new RobocopyPlan
                {
                    NeedsFollowUp = false,
                    Source = source,
                    Destination = destination,
                    Options = options,
                    Explanation = GetString(root, "explanation"),
                    Warnings = BuildWarnings(options, GetStringArray(root, "warnings")),
                    CommandLine = RobocopyCommand.Render(source, destination, options),
                };

                return true;
            }
        }

        private bool TryParseOptions(JsonElement root, out List<RobocopyPlanOption> options, out string validationError)
        {
            options = [];
            validationError = string.Empty;

            if (!root.TryGetProperty("options", out var optionsElement) || optionsElement.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (optionsElement.ValueKind != JsonValueKind.Array)
            {
                validationError = "options must be an array";
                return false;
            }

            foreach (var element in optionsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    validationError = "each entry in options must be an object";
                    return false;
                }

                var name = GetString(element, "name").Trim();
                var value = GetString(element, "value").Trim();

                if (string.IsNullOrEmpty(name))
                {
                    validationError = "an option was missing its name";
                    return false;
                }

                // Tolerate "/R:3" being supplied as a single name.
                var colonIndex = name.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex > 0)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        value = name[(colonIndex + 1)..].Trim();
                    }

                    name = name[..colonIndex];
                }

                if (!name.StartsWith('/'))
                {
                    name = "/" + name;
                }

                if (!_catalogByName.TryGetValue(name, out var candidates))
                {
                    validationError = $"'{name}' is not a supported switch. Use only switches from the list; the closest supported ones are {SuggestAlternatives(name)}";
                    return false;
                }

                var descriptor = SelectDescriptor(candidates, value);
                if (descriptor is null)
                {
                    validationError = $"'{value}' is not a valid value for {name}";
                    return false;
                }

                if (!IsValueValid(descriptor, value, out var valueError))
                {
                    validationError = $"'{name}' {valueError}. Correct form: {UsageForm(descriptor)}";
                    return false;
                }

                if (options.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                options.Add(new RobocopyPlanOption(name, value));
            }

            RobocopyCommand.Prune(options);

            return true;
        }

        private static RobocopyOptionDescriptor? SelectDescriptor(List<RobocopyOptionDescriptor> candidates, string value)
        {
            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            // A switch name can map to more than one control (e.g. /LFSM as a flag and as a storage
            // option). Pick the variant that matches the supplied value.
            var wantsFlag = string.IsNullOrEmpty(value);
            return candidates.FirstOrDefault(candidate => (candidate.Kind == RobocopyOptionKind.Flag) == wantsFlag)
                   ?? candidates[0];
        }

        private static bool IsValueValid(RobocopyOptionDescriptor descriptor, string value, out string error)
        {
            error = string.Empty;

            switch (descriptor.Kind)
            {
                case RobocopyOptionKind.Flag:
                    if (!string.IsNullOrEmpty(value))
                    {
                        error = "does not take a value, so \"value\" must be an empty string";
                        return false;
                    }

                    return true;

                case RobocopyOptionKind.Number:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        error = $"needs a whole number but got '{value}'";
                        return false;
                    }

                    return true;

                case RobocopyOptionKind.Storage:
                    if (value.Length < 2
                        || !"KMG".Contains(char.ToUpperInvariant(value[^1]), StringComparison.Ordinal)
                        || !int.TryParse(value[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        error = $"needs a whole number followed by K, M or G but got '{value}'";
                        return false;
                    }

                    return true;

                case RobocopyOptionKind.Text:
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "needs a non-empty value";
                        return false;
                    }

                    return true;

                case RobocopyOptionKind.MultiSelect:
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "needs one or more letters";
                        return false;
                    }

                    foreach (var letter in value)
                    {
                        if (!descriptor.AllowedValueLetters.Any(allowed => allowed.Length == 1 && char.ToUpperInvariant(allowed[0]) == char.ToUpperInvariant(letter)))
                        {
                            error = $"uses letter '{letter}', but only {string.Concat(descriptor.AllowedValueLetters)} are allowed";
                            return false;
                        }
                    }

                    return true;

                case RobocopyOptionKind.RunHours:
                    if (!IsRunHoursValid(value))
                    {
                        error = $"needs a time window as hhmm-hhmm but got '{value}'";
                        return false;
                    }

                    return true;

                default:
                    return true;
            }
        }

        private static bool IsRunHoursValid(string value)
        {
            var parts = value.Split('-');
            if (parts.Length != 2)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (part.Length != 4
                    || !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    return false;
                }

                var hours = int.Parse(part[..2], CultureInfo.InvariantCulture);
                var minutes = int.Parse(part[2..], CultureInfo.InvariantCulture);

                if (hours > 23 || minutes > 59)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Pulls the first complete JSON object out of a response, tolerating markdown fences and
        /// surrounding prose. Braces are matched (ignoring those inside strings) so that trailing
        /// commentary or a second object does not corrupt the result.
        /// </summary>
        private static string ExtractJson(string raw)
        {
            var start = raw.IndexOf('{', StringComparison.Ordinal);
            if (start < 0)
            {
                return null!;
            }

            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = start; i < raw.Length; i++)
            {
                var c = raw[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;

                    case '{':
                        depth++;
                        break;

                    case '}':
                        depth--;
                        if (depth == 0)
                        {
                            return raw[start..(i + 1)];
                        }

                        break;
                }
            }

            // Unbalanced, most likely a truncated response; fall back to the widest span.
            var end = raw.LastIndexOf('}');
            return end > start ? raw[start..(end + 1)] : null!;
        }

        private static string GetString(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return value.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();
        }

        /// <summary>
        /// Warnings about data loss are a safety feature, so they cannot be left to the model's
        /// discretion - small models omit them roughly half the time. Any destructive switch gets a
        /// deterministic warning here, and the model's own warnings are kept alongside it.
        /// </summary>
        private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<RobocopyPlanOption> options, IReadOnlyList<string> modelWarnings)
        {
            var warnings = new List<string>();

            warnings.AddRange(RobocopyCommand.GetDestructiveWarnings(options));

            foreach (var warning in modelWarnings)
            {
                if (!warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
                {
                    warnings.Add(warning);
                }
            }

            return warnings;
        }
    }
}
