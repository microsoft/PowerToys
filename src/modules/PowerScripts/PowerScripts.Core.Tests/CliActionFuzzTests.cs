// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.FuzzTests;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class CliActionFuzzTests
{
    private const string ValidAction = """{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"parameter":"path"},"json":{"constant":true}}}""";
    private const string ValidTemplate = """{"projectName":"Find file usage","operation":{"commandId":"filelocksmith.query","bindings":{"path":{"parameter":"path"},"json":{"constant":true}}}}""";

    [TestMethod]
    public void ValidSeedsReachProductionConfigurationValidation()
    {
        Assert.AreEqual(FuzzInputResult.Accepted, CliActionFuzzer.ParseAndValidateAction(Encoding.UTF8.GetBytes(ValidAction)));
        Assert.AreEqual(FuzzInputResult.Accepted, CliActionFuzzer.ParseTemplateConfiguration(Encoding.UTF8.GetBytes(ValidTemplate)));
    }

    [TestMethod]
    public void EntryPointsConsumeCheckedInCorpusAndEachTruncation()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "FuzzSeeds");
        var seeds = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
        Assert.IsTrue(seeds.Length >= 9, "The checked-in action and template corpora must be deployed to the test output.");

        foreach (var seed in seeds)
        {
            var bytes = File.ReadAllBytes(seed);
            InvokeBoth(bytes);
            for (var length = 0; length < bytes.Length; length++)
            {
                InvokeBoth(bytes.AsSpan(0, length));
            }
        }
    }

    [TestMethod]
    public void RejectedCasesIncludeDuplicateKeysUnknownMembersAndInvalidDepth()
    {
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseAndValidateAction(Encoding.UTF8.GetBytes(
            """{"schemaVersion":1,"commandId":"filelocksmith.query","commandId":"fancyzones.get-layouts","bindings":{}}""")));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseAndValidateAction(Encoding.UTF8.GetBytes(
            """{"schemaVersion":1,"commandId":"filelocksmith.query","bindings":{"path":{"parameter":"path"}},"executable":"cmd.exe"}""")));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseTemplateConfiguration(Encoding.UTF8.GetBytes(
            """{"projectName":"First","projectName":"Second"}""")));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseTemplateConfiguration(Encoding.UTF8.GetBytes(
            """{"projectName":"Valid","unknown":false}""")));

        var nested = new string('[', 80) + "0" + new string(']', 80);
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseAndValidateAction(Encoding.UTF8.GetBytes(nested)));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseTemplateConfiguration(Encoding.UTF8.GetBytes(nested)));
    }

    [TestMethod]
    public void FixedSeedMutationAndInvalidUtf8DoNotHideUnexpectedParserFailures()
    {
        var random = new Random(0x5C71);
        var seeds = new[] { Encoding.UTF8.GetBytes(ValidAction), Encoding.UTF8.GetBytes(ValidTemplate) };
        for (var iteration = 0; iteration < 1_024; iteration++)
        {
            var mutated = seeds[iteration % seeds.Length].ToArray();
            for (var mutation = 0; mutation <= iteration % 8; mutation++)
            {
                mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
            }

            InvokeBoth(mutated);
            var arbitrary = new byte[random.Next(0, 512)];
            random.NextBytes(arbitrary);
            InvokeBoth(arbitrary);
        }

        InvokeBoth(new byte[] { 0xC0, 0xAF, 0xF5, 0xFF, 0x00, 0x80 });
        InvokeBoth(Encoding.UTF8.GetBytes("""{"projectName":"你好 😀 \u0000","operation":null}"""));
    }

    [TestMethod]
    public void InputsOverHarnessBudgetAreSkippedBeforeAllocation()
    {
        var input = new byte[CliActionFuzzer.MaximumInputBytes + 1];
        Assert.AreEqual(FuzzInputResult.SkippedOversize, CliActionFuzzer.ParseAndValidateAction(input));
        Assert.AreEqual(FuzzInputResult.SkippedOversize, CliActionFuzzer.ParseTemplateConfiguration(input));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseTemplateConfiguration(
            Encoding.UTF8.GetBytes(new string(' ', 16_385))));
        Assert.AreEqual(FuzzInputResult.Rejected, CliActionFuzzer.ParseAndValidateAction(
            Encoding.UTF8.GetBytes(new string(' ', (256 * 1024) + 1))));
    }

    private static void InvokeBoth(ReadOnlySpan<byte> input)
    {
        CliActionFuzzer.FuzzAction(input);
        CliActionFuzzer.FuzzTemplateConfiguration(input);
    }
}
