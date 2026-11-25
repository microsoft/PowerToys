// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class AdvancedPasteCliModule : PowerShellCliModule
{
    public override string Id => "advanced-paste";

    public override string Name => "Advanced Paste";

    public override string IconName => "AdvancedPaste";

    public override string Executable => @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe";

    public override int Order => 10;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(Query("advanced-paste-local", "Advanced Paste: transform text locally",
            "Convert text to plain text, Markdown or JSON using the local transformation engine. Uses standard input/output only; no clipboard, package identity, saved credentials, AI or network access.", "text",
            CliArgument.Option("--format", Text("Format", "Local text transformation.", "plain-text", "plain-text", "markdown", "json")))
            with { Executable = @"WinUI3Apps\PowerToys.AdvancedPaste.LocalCLI.exe", StandardInput = "$Content" },
            "--format plain-text|markdown|json; stdin"),
        Describe(Template("advanced-paste-list", "Advanced Paste: list actions or providers",
            "Read-only discovery. Does not read the clipboard or contact AI providers.", "none",
            """
            $__powerscriptArguments.AddRange([string[]]@($ListKind, 'list'))
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("ListKind", "Which catalogue to list.", "actions", "actions", "providers"),
            Flag("Json", "Return structured JSON.", true)),
            "actions | providers list [--json]"),
        Describe(Template("advanced-paste-file-text", "Advanced Paste: convert a file to text or extract OCR",
            "Read one text/HTML/image file and return plain text, Markdown, JSON text or Windows OCR text. No AI provider or clipboard access.", "files",
            """
            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $Files[0], '--stdout'))
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Action", "Text-producing transformation; input must support the chosen action.", "plain-text", "plain-text", "markdown", "json", "image-to-text"),
            Flag("Json", "Return the CLI's structured report as text.")),
            "transform --action plain-text|markdown|json|image-to-text --input --stdout"),
        Describe(AdvancedPasteFileResult(),
            "transform --action paste-as-*-file|transcode-to-mp3|transcode-to-mp4 --input --output"),
        Describe(AdvancedPasteTextFile(),
            "transform --action paste-as-txt-file --stdin --output"),
        Describe(AdvancedPasteHtml(files: false),
            "transform --action plain-text|markdown|json --input <staged HTML> --stdout"),
        Describe(AdvancedPasteHtml(files: true),
            "transform --action paste-as-html-file --input <staged HTML> --output"),
        Describe(AdvancedPasteAi(input: "text", files: false),
            "transform --action fix-spelling-and-grammar|paste-with-ai | --custom-action; --stdin --stdout"),
        Describe(AdvancedPasteAi(input: "files", files: false),
            "transform --action fix-spelling-and-grammar|paste-with-ai | --custom-action; --input --stdout"),
        Describe(AdvancedPasteAi(input: "text", files: true),
            "transform --action fix-spelling-and-grammar|paste-with-ai | --custom-action; --stdin --output"),
        Describe(AdvancedPasteAi(input: "files", files: true),
            "transform --action fix-spelling-and-grammar|paste-with-ai | --custom-action; --input --output"),
    ];

    private PowerShellCliTemplate AdvancedPasteFileResult() =>
        Template("advanced-paste-file-result", "Advanced Paste: create an image, document or media file",
            "Transform one supported input file into TXT, PNG, JPG, HTML, MP3 or MP4. Return a new workspace file; the consumer owns saving it. No clipboard or host destination.", "files",
            """
            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            """ + "\n" + WorkspaceDirectory + """

            $__powerscriptExtension = switch ($Action) {
                'paste-as-txt-file' { 'txt' }
                'paste-as-png-file' { 'png' }
                'paste-as-jpg-file' { 'jpg' }
                'paste-as-html-file' { 'html' }
                'transcode-to-mp3' { 'mp3' }
                'transcode-to-mp4' { 'mp4' }
            }
            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory ('result.' + $__powerscriptExtension)
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $Files[0], '--output', $__powerscriptResultFile))
            """,
            Text("Action", "Input must support the chosen transformation.", "paste-as-txt-file",
                "paste-as-txt-file", "paste-as-png-file", "paste-as-jpg-file", "paste-as-html-file", "transcode-to-mp3", "transcode-to-mp4"))
            with
        { Output = "files", OutputScript = FileOutput };

    private PowerShellCliTemplate AdvancedPasteTextFile() =>
        Template("advanced-paste-text-file", "Advanced Paste: save text as a new TXT file",
            "Convert supplied text into a returned workspace TXT file; does not overwrite a host destination or read the clipboard.", "text",
            WorkspaceDirectory + """

            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory 'result.txt'
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', 'paste-as-txt-file', '--stdin', '--output', $__powerscriptResultFile))
            """)
            with
        { Output = "files", StandardInput = "$Content", OutputScript = FileOutput };

    private PowerShellCliTemplate AdvancedPasteHtml(bool files) =>
        Template(files ? "advanced-paste-html-file" : "advanced-paste-html-text",
            files ? "Advanced Paste: save HTML as a new HTML file" : "Advanced Paste: convert supplied HTML to text",
            "Stage supplied HTML as an explicit UTF-8 .html input so the full CLI sees HTML, not only stdin plain text. New files stay in the workspace.", "text",
            WorkspaceDirectory + """

            $__powerscriptHtmlInput = Join-Path $__powerscriptResultDirectory 'input.html'
            [IO.File]::WriteAllText($__powerscriptHtmlInput, $Content, [Text.UTF8Encoding]::new($false))
            """ + (files ? """

            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory 'result.html'
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', 'paste-as-html-file', '--input', $__powerscriptHtmlInput, '--output', $__powerscriptResultFile))
            """ : """

            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $__powerscriptHtmlInput, '--stdout'))
            """),
            files ? [] : [Text("Action", "Text-producing HTML transformation.", "markdown", "markdown", "plain-text", "json")])
            with
        { Output = files ? "files" : "text", OutputScript = files ? FileOutput : null };

    private PowerShellCliTemplate AdvancedPasteAi(string input, bool files) =>
        Template($"advanced-paste-ai-{input}-{(files ? "file" : "text")}", $"Advanced Paste: AI {input} to {(files ? "a file" : "text")}",
            "Run spelling/grammar, a prompt or a saved custom action with the configured provider. Content may leave the machine and incur costs. Text output rejects file-only results; file output saves a single result to the workspace.", input,
            """
            $__powerscriptArguments.Add('transform')
            if ($CustomAction) { $__powerscriptArguments.AddRange([string[]]@('--custom-action', $CustomAction)) }
            else {
                $__powerscriptArguments.AddRange([string[]]@('--action', $Action))
                if ($Action -eq 'paste-with-ai' -and -not $Prompt.Trim()) { throw 'AI paste requires Prompt.' }
            }
            if ($Prompt) { $__powerscriptArguments.AddRange([string[]]@('--prompt', $Prompt)) }
            if ($Provider) { $__powerscriptArguments.AddRange([string[]]@('--provider', $Provider)) }
            """ + (input == "files" ? """

            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            $__powerscriptArguments.AddRange([string[]]@('--input', $Files[0]))
            """ : "\n$__powerscriptArguments.Add('--stdin')") +
            (files ? "\n" + WorkspaceDirectory + """

            if (-not $FileName.Trim() -or $FileName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $FileName -in @('.', '..')) { throw 'FileName must be a single valid filename, not a path.' }
            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory $FileName
            $__powerscriptArguments.AddRange([string[]]@('--output', $__powerscriptResultFile))
            """ : "\n$__powerscriptArguments.Add('--stdout')"),
            [
                Text("Action", "Ignored when CustomAction is specified.", "fix-spelling-and-grammar", "fix-spelling-and-grammar", "paste-with-ai"),
                Text("CustomAction", "Optional saved action ID or name from actions list. Overrides Action."),
                Text("Prompt", "Required by paste-with-ai unless a saved custom action is selected."),
                Text("Provider", "Optional configured provider ID; no API key is stored in this script."),
                .. files ? new[] { Text("FileName", "Single output filename with the expected extension for the configured action.", "result.txt") } : [],
            ])
            with
        { UsesNetwork = true, Output = files ? "files" : "text", StandardInput = input == "text" ? "$Content" : null, OutputScript = files ? FileOutput : null };
}
