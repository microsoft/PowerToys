# PowerScripts configuration fuzz targets

This dependency-free .NET 10 library exposes two libfuzzer-dotnet targets:

- CliActionFuzzer.FuzzAction parses an action.json input and validates it against a representative standard file-query descriptor.
- CliActionFuzzer.FuzzTemplateConfiguration parses the configured-template JSON request.

Both entry points accept ReadOnlySpan<byte>, decode the supplied bytes as UTF-8, and cap an iteration at 512 KiB, above the action parser's 256 KiB and template parser's 16 KiB limits. They call the production parsers and do not create packages, modify trust, read arbitrary files, or launch any process. Only expected JsonException and InvalidDataException rejection results are caught; unexpected exceptions remain visible to the fuzzing engine.

Seeds/Actions and Seeds/Templates contain valid and malformed starting inputs. PowerScripts.Core.Tests/CliActionFuzzTests.cs reuses this actual assembly for deterministic corpus, truncation, mutation, invalid UTF-8, depth, and size smoke coverage. These unit tests are a bounded regression check, not a coverage-guided fuzzing campaign.

The project intentionally targets .NET 10, matching PowerScripts.Core; it does not import the repository's older .NET 8 fuzzing override. OneFuzzConfig.json follows the repository's v3 entry format and leaves the jobs skipped. The cloud runner's .NET 10 compatibility, ingestion, and work-item configuration have not been verified here. No cloud job, notification, or work item is created by these files. Deployment can be enabled by the owning pipeline after that configuration is verified.

The assembly and seeds are emitted under x64/Debug/tests/PowerScripts.FuzzTests/net10.0 for an x64 Debug build. See doc/devdocs/tools/fuzzingtesting.md for the repository's instrumentation and cloud workflow. No coverage-guided local or cloud run is implied by adding these targets.
