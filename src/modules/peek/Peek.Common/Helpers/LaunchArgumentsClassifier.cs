// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Peek.Common.Helpers
{
    public static class LaunchArgumentsClassifier
    {
        public const string RunnerPidArgumentName = "--runner-pid";

        private static readonly IReadOnlyList<string> EmptyCliArguments = [];

        public enum ClassificationMode
        {
            None,
            Runner,
            Cli,
            InvalidRunnerArguments,
        }

        public readonly record struct Classification(ClassificationMode Mode, int RunnerPid, IReadOnlyList<string> CliArguments)
        {
            public static Classification None { get; } =
                new(ClassificationMode.None, 0, EmptyCliArguments);

            public static Classification InvalidRunnerArguments { get; } =
                new(ClassificationMode.InvalidRunnerArguments, 0, EmptyCliArguments);

            public static Classification CreateRunner(int runnerPid) =>
                new(ClassificationMode.Runner, runnerPid, EmptyCliArguments);

            public static Classification CreateCli(IReadOnlyList<string> cliArguments) =>
                new(ClassificationMode.Cli, 0, cliArguments);
        }

        public static Classification Classify(IReadOnlyList<string>? launchArgs)
        {
            if (launchArgs is null || launchArgs.Count == 0)
            {
                return Classification.None;
            }

            // Keep Runner and CLI activation unambiguous: only "--runner-pid <pid>" is treated
            // as Runner input, and all other argument shapes are interpreted as CLI paths.
            if (string.Equals(launchArgs[0], RunnerPidArgumentName, StringComparison.OrdinalIgnoreCase))
            {
                return launchArgs.Count == 2 && int.TryParse(launchArgs[1], out int runnerPid)
                    ? Classification.CreateRunner(runnerPid)
                    : Classification.InvalidRunnerArguments;
            }

            return Classification.CreateCli(launchArgs);
        }
    }
}
