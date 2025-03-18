// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Services;
using AdvancedPaste.Settings;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class CustomActionKernelQueryCacheServiceTests
{
    private static readonly CacheKey CustomActionTestKey = new() { Prompt = "TestPrompt1", AvailableFormats = ClipboardFormat.Text };
    private static readonly CacheKey CustomActionTestKey2 = new() { Prompt = "TestPrompt2", AvailableFormats = ClipboardFormat.File | ClipboardFormat.Image };
    private static readonly CacheKey MarkdownTestKey = new() { Prompt = "Paste as Markdown", AvailableFormats = ClipboardFormat.Text };
    private static readonly CacheKey JSONTestKey = new() { Prompt = "Paste as JSON", AvailableFormats = ClipboardFormat.Text };
    private static readonly CacheKey PasteAsTxtFileKey = new() { Prompt = "Paste as .txt file", AvailableFormats = ClipboardFormat.File };
    private static readonly CacheKey PasteAsPngFileKey = new() { Prompt = "Paste as .png file", AvailableFormats = ClipboardFormat.Image };

    private static readonly CacheValue TestValue = new([new(PasteFormats.PlainText, [])]);
    private static readonly CacheValue TestValue2 = new([new(PasteFormats.KernelQuery, new() { { "a", "b" }, { "c", "d" } })]);

    private CustomActionKernelQueryCacheService _cacheService;
    private Mock<IUserSettings> _userSettings;
    private MockFileSystem _fileSystem;

    [TestInitialize]
    public void TestInitialize()
    {
        _userSettings = new();
        UpdateUserActions([], []);

        _fileSystem = new();
        _cacheService = new(_userSettings.Object, _fileSystem);
    }

    [TestMethod]
    public async Task Test_Cache_Always_Accepts_Core_Action_Prompt()
    {
        await AssertAcceptsAsync(MarkdownTestKey);
    }

    [TestMethod]
    public async Task Test_Cache_Accepts_Prompt_When_Custom_Action()
    {
        await AssertRejectsAsync(CustomActionTestKey);

        UpdateUserActions([], [new() { Name = nameof(CustomActionTestKey), Prompt = CustomActionTestKey.Prompt, IsShown = true }]);

        await AssertAcceptsAsync(CustomActionTestKey);
        await AssertRejectsAsync(CustomActionTestKey2, PasteAsTxtFileKey);

        UpdateUserActions([], []);
        await AssertRejectsAsync(CustomActionTestKey);
    }

    [TestMethod]
    public async Task Test_Cache_Accepts_Prompt_When_User_Additional_Action()
    {
        await AssertRejectsAsync(PasteAsTxtFileKey, PasteAsPngFileKey);

        UpdateUserActions([PasteFormats.PasteAsHtmlFile, PasteFormats.PasteAsTxtFile], []);

        await AssertAcceptsAsync(PasteAsTxtFileKey);
        await AssertRejectsAsync(PasteAsPngFileKey, CustomActionTestKey);

        UpdateUserActions([], []);
        await AssertRejectsAsync(PasteAsTxtFileKey);
    }

    [TestMethod]
    public async Task Test_Cache_Overwrites_Latest_Value()
    {
        await _cacheService.WriteAsync(JSONTestKey, TestValue);
        await _cacheService.WriteAsync(MarkdownTestKey, TestValue2);

        await _cacheService.WriteAsync(JSONTestKey, TestValue2);
        await _cacheService.WriteAsync(MarkdownTestKey, TestValue);

        AssertAreEqual(TestValue2, _cacheService.ReadOrNull(JSONTestKey));
        AssertAreEqual(TestValue, _cacheService.ReadOrNull(MarkdownTestKey));
    }

    [TestMethod]
    public async Task Test_Cache_Uses_Case_Insensitive_Prompt_Comparison()
    {
        static CacheKey CreateUpperCaseKey(CacheKey key) =>
            new() { Prompt = key.Prompt.ToUpperInvariant(), AvailableFormats = key.AvailableFormats };

        await _cacheService.WriteAsync(CreateUpperCaseKey(JSONTestKey), TestValue);
        await _cacheService.WriteAsync(MarkdownTestKey, TestValue2);

        AssertAreEqual(TestValue, _cacheService.ReadOrNull(JSONTestKey));
        AssertAreEqual(TestValue2, _cacheService.ReadOrNull(MarkdownTestKey));
    }

    [TestMethod]
    public async Task Test_Cache_Uses_Clipboard_Formats_In_Key()
    {
        CacheKey key1 = new() { Prompt = JSONTestKey.Prompt, AvailableFormats = ClipboardFormat.File };
        CacheKey key2 = new() { Prompt = JSONTestKey.Prompt, AvailableFormats = ClipboardFormat.Image };

        await _cacheService.WriteAsync(key1, TestValue);

        Assert.IsNotNull(_cacheService.ReadOrNull(key1));
        Assert.IsNull(_cacheService.ReadOrNull(key2));
    }

    [TestMethod]
    public async Task Test_Cache_Is_Persistent()
    {
        await _cacheService.WriteAsync(JSONTestKey, TestValue);
        await _cacheService.WriteAsync(MarkdownTestKey, TestValue2);

        _cacheService = new(_userSettings.Object, _fileSystem); // recreate using same mock file-system to simulate app restart

        AssertAreEqual(TestValue, _cacheService.ReadOrNull(JSONTestKey));
        AssertAreEqual(TestValue2, _cacheService.ReadOrNull(MarkdownTestKey));
    }

    private async Task AssertRejectsAsync(params CacheKey[] keys)
    {
        foreach (var key in keys)
        {
            Assert.IsNull(_cacheService.ReadOrNull(key));
            await _cacheService.WriteAsync(key, TestValue);
            Assert.IsNull(_cacheService.ReadOrNull(key));
        }
    }

    private async Task AssertAcceptsAsync(params CacheKey[] keys)
    {
        foreach (var key in keys)
        {
            Assert.IsNull(_cacheService.ReadOrNull(key));
            await _cacheService.WriteAsync(key, TestValue);
            AssertAreEqual(TestValue, _cacheService.ReadOrNull(key));
        }
    }

    private static void AssertAreEqual(CacheValue valueA, CacheValue valueB)
    {
        Assert.IsNotNull(valueA);
        Assert.IsNotNull(valueB);

        Assert.AreEqual(valueA.ActionChain.Count, valueB.ActionChain.Count);

        foreach (var (itemA, itemB) in valueA.ActionChain.Zip(valueB.ActionChain))
        {
            Assert.AreEqual(itemA.Format, itemB.Format);
            Assert.AreEqual(itemA.Arguments.Count, itemB.Arguments.Count);
            Assert.IsFalse(itemA.Arguments.Except(itemB.Arguments).Any());
        }
    }

    private void UpdateUserActions(PasteFormats[] additionalActions, AdvancedPasteCustomAction[] customActions)
    {
        _userSettings.Setup(settingsObj => settingsObj.AdditionalActions).Returns(additionalActions);
        _userSettings.Setup(settingsObj => settingsObj.CustomActions).Returns(customActions);
        _userSettings.Raise(settingsObj => settingsObj.Changed += null, EventArgs.Empty);
    }
}
