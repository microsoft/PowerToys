// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests
{
    // The module interface and the settings page talk to each other over a named pipe with JSON.
    // These tests stand in for the module interface, so that the shape of the messages is held still.
    [TestClass]
    public class SoundManagerPipeClientTests
    {
        private const string FirefoxPath = "C:\\Program Files\\Mozilla Firefox\\firefox.exe";

        // Every test gets a pipe of its own, so that the tests do not wait for each other.
        private static int _lastRunnerProcessId = 900000;

        [TestMethod]
        public void ThePipeNameCarriesTheNumberOfTheRunner()
        {
            Assert.AreEqual("PowerToys-SoundManager-1234", SoundManagerPipeClient.GetPipeName(1234));
        }

        [TestMethod]
        public async Task TheProgramsOfTheSnapshotAreRead()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App(FirefoxPath, "firefox", false)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(1, programs.Count);
            Assert.AreEqual("firefox.exe", programs[0].Program);
            Assert.AreEqual("firefox", programs[0].Name);
            Assert.AreEqual(FirefoxPath, programs[0].ProcessPath);
        }

        [TestMethod]
        public async Task TheSnapshotIsAskedFor()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot());
            using var client = new SoundManagerPipeClient();

            await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual("get_snapshot", CommandOf(await module.Request));
        }

        [TestMethod]
        public async Task AProgramThatMakesSoundInTwoWaysIsInTheListOnce()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App(FirefoxPath, "firefox", false), App(FirefoxPath, "firefox", false)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(1, programs.Count);
        }

        [TestMethod]
        public async Task ARecordingIsLeftOut()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App(FirefoxPath, "firefox", false), App(FirefoxPath, "firefox", true)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(1, programs.Count);
            Assert.AreEqual("firefox.exe", programs[0].Program);
        }

        [TestMethod]
        public async Task TheProgramsAreSortedByName()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App("C:\\apps\\zebra.exe", "zebra", false), App("C:\\apps\\alpha.exe", "alpha", false)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(2, programs.Count);
            Assert.AreEqual("alpha", programs[0].Name);
            Assert.AreEqual("zebra", programs[1].Name);
        }

        [TestMethod]
        public async Task TheSoundOfTheSystemIsLeftOut()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App(string.Empty, "System Sounds", false)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(0, programs.Count);
        }

        [TestMethod]
        public async Task AProgramWithoutAPathKeepsItsName()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), Snapshot(App(string.Empty, "SomeApp", false)));
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(1, programs.Count);
            Assert.AreEqual("SomeApp", programs[0].Program);
            Assert.AreEqual("SomeApp", programs[0].Name);
            Assert.AreEqual(string.Empty, programs[0].ProcessPath);
        }

        [TestMethod]
        public async Task AnAnswerWithoutProgramsGivesNone()
        {
            var answer = new JsonObject
            {
                ["outputs"] = new JsonArray(),
                ["inputs"] = new JsonArray(),
                ["testingDeviceId"] = string.Empty,
            };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var programs = await client.GetProgramsAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(0, programs.Count);
        }

        [TestMethod]
        public async Task TheChoiceOfWindowsIsRead()
        {
            var answer = new JsonObject { ["ducking"] = 2 };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var mode = await client.GetDuckingModeAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(2, mode);
        }

        [TestMethod]
        public async Task TheChoiceOfWindowsIsAskedFor()
        {
            var answer = new JsonObject { ["ducking"] = 2 };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            await client.GetDuckingModeAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual("get_ducking", CommandOf(await module.Request));
        }

        [TestMethod]
        public async Task AnAnswerWithoutAChoiceGivesMinusOne()
        {
            using var module = new FakeModuleInterface(NewRunnerProcessId(), new JsonObject().ToJsonString());
            using var client = new SoundManagerPipeClient();

            var mode = await client.GetDuckingModeAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(-1, mode);
        }

        [TestMethod]
        public async Task AChoiceThatIsNotANumberGivesMinusOne()
        {
            var answer = new JsonObject { ["ducking"] = "two" };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var mode = await client.GetDuckingModeAsync(module.RunnerProcessId, CancellationToken.None);

            Assert.AreEqual(-1, mode);
        }

        [TestMethod]
        public async Task TheChoiceOfWindowsIsTold()
        {
            var answer = new JsonObject { ["id"] = 0, ["ok"] = true };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var ok = await client.SetDuckingModeAsync(module.RunnerProcessId, 1, CancellationToken.None);

            Assert.IsTrue(ok);

            var request = JsonNode.Parse(await module.Request);
            Assert.AreEqual("set_ducking", request?["command"]?.GetValue<string>());
            Assert.AreEqual(1, request?["value"]?.GetValue<int>());
        }

        [TestMethod]
        public async Task AChangeThatWindowsRefusedGivesFalse()
        {
            var answer = new JsonObject { ["id"] = 0, ["ok"] = false };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var ok = await client.SetDuckingModeAsync(module.RunnerProcessId, 1, CancellationToken.None);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public async Task AnAnswerWithoutAResultGivesFalse()
        {
            var answer = new JsonObject { ["id"] = 0 };

            using var module = new FakeModuleInterface(NewRunnerProcessId(), answer.ToJsonString());
            using var client = new SoundManagerPipeClient();

            var ok = await client.SetDuckingModeAsync(module.RunnerProcessId, 1, CancellationToken.None);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public async Task TheCallsFailQuietlyWhenTheModuleInterfaceIsNotThere()
        {
            using var client = new SoundManagerPipeClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            var programs = await client.GetProgramsAsync(987654, timeout.Token);
            var mode = await client.GetDuckingModeAsync(987654, timeout.Token);
            var ok = await client.SetDuckingModeAsync(987654, 1, timeout.Token);

            Assert.AreEqual(0, programs.Count);
            Assert.AreEqual(-1, mode);
            Assert.IsFalse(ok);
        }

        private static int NewRunnerProcessId()
        {
            return Interlocked.Increment(ref _lastRunnerProcessId);
        }

        // The name of the command that was asked for.
        private static string CommandOf(string request)
        {
            return JsonNode.Parse(request)?["command"]?.GetValue<string>() ?? string.Empty;
        }

        // The answer of the module interface, with the programs that are making sound in it.
        private static string Snapshot(params string[] apps)
        {
            var list = new JsonArray();

            foreach (var app in apps)
            {
                list.Add(JsonNode.Parse(app));
            }

            var root = new JsonObject
            {
                ["outputs"] = new JsonArray(),
                ["inputs"] = new JsonArray(),
                ["apps"] = list,
                ["testingDeviceId"] = string.Empty,
            };

            return root.ToJsonString();
        }

        // One program of the answer of the module interface, in the shape that the module interface writes it in.
        private static string App(string processPath, string displayName, bool isCapture)
        {
            var app = new JsonObject
            {
                ["sessionId"] = "1",
                ["processPath"] = processPath,
                ["displayName"] = displayName,
                ["processId"] = 1,
                ["volume"] = 0.5,
                ["muted"] = false,
                ["isActive"] = true,
                ["isCapture"] = isCapture,
                ["outputDeviceId"] = string.Empty,
                ["balanceLeft"] = 1,
                ["balanceRight"] = 1,
            };

            return app.ToJsonString();
        }

        // Stands in for the module interface, so that the tests can see what was asked and answer it.
        private sealed class FakeModuleInterface : IDisposable
        {
            private readonly NamedPipeServerStream _pipe;

            private readonly Task<string> _request;

            public FakeModuleInterface(int runnerProcessId, string answer)
            {
                RunnerProcessId = runnerProcessId;

                _pipe = new NamedPipeServerStream(
                    SoundManagerPipeClient.GetPipeName(runnerProcessId),
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _request = Task.Run(async () => await AnswerAsync(answer));
            }

            public int RunnerProcessId { get; }

            // What the settings page asked for, in the shape it was written in.
            public Task<string> Request => _request;

            public void Dispose()
            {
                _pipe.Dispose();
            }

            private async Task<string> AnswerAsync(string answer)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _pipe.WaitForConnectionAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // Nobody came to the pipe, so there is nothing to answer.
                    return string.Empty;
                }

                using var reader = new StreamReader(_pipe, Encoding.UTF8, false, 1024, true);

                var request = await reader.ReadLineAsync();

                try
                {
                    await _pipe.WriteAsync(Encoding.UTF8.GetBytes(answer + "\n"));
                    await _pipe.FlushAsync();
                }
                catch (IOException)
                {
                    // The settings page went away before the answer was written, which is what happens when it is closed while it waits.
                }

                return request ?? string.Empty;
            }
        }
    }
}
