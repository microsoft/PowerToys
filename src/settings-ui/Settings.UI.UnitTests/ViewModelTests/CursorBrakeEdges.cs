// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Xml.Linq;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class CursorBrakeEdges
    {
        private const string Hp = @"\\?\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352";
        private const string Dell = @"\\?\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353";
        private const string Lenovo = @"\\?\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354";
        private const string Lg = @"\\?\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355";
        private const string DetectedPath = @"C:\Settings\CursorBrake\detected-edges.json";

        // ComboBox positions: the edge row's five items, of which a direction row has the first four.
        private const int DefaultItem = 0;
        private const int CustomItem = 1;
        private const int OffItem = 2;
        private const int WallItem = 3;
        private const int DifferentEachWayItem = 4;

        // HP (0, main) | DELL (1), LENOVO (2) above both, which Windows numbers 1, 3 and 2. Edges A = HP-DELL,
        // B = HP-LENOVO and C = DELL-LENOVO, in the order the module publishes them. Every pair
        // and LG with HP are in `seen`.
        private const string Fixture = """
            {
              "version": 1,
              "identified": true,
              "monitors": [
                { "id": "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "name": "HP E24 G5", "rect": [0, 0, 1920, 1080], "dpi": 96, "primary": true, "number": 1 },
                { "id": "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353", "name": "Dell U2724D", "rect": [1920, 0, 4480, 1440], "dpi": 144, "primary": false, "number": 3, "built_in": true },
                { "id": "\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354", "name": "Lenovo L32p-30", "rect": [0, -2160, 3840, 0], "dpi": 192, "primary": false, "number": 2 }
              ],
              "edges": [
                { "monitors": [0, 1], "axis": "vertical", "at": 1920, "from": 0, "to": 1080 },
                { "monitors": [0, 2], "axis": "horizontal", "at": 0, "from": 0, "to": 1920 },
                { "monitors": [1, 2], "axis": "horizontal", "at": 0, "from": 1920, "to": 3840 }
              ],
              "seen": [
                { "monitors": ["\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353"], "last_seen_utc": "2026-10-14T08:00:00Z" },
                { "monitors": ["\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354"], "last_seen_utc": "2026-10-14T08:00:00Z" },
                { "monitors": ["\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353", "\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354"], "last_seen_utc": "2026-10-14T08:00:00Z" },
                { "monitors": ["\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355"], "last_seen_utc": "2026-10-02T08:00:00Z" }
              ]
            }
            """;

        // The English strings the view model formats, so the tests read what a user reads.
        private static readonly Dictionary<string, string> Strings = new()
        {
            ["CursorBrake_Edges_DefaultItem"] = "Default ({0} px)",
            ["CursorBrake_Edges_DefaultOffItem"] = "Default (Off)",
            ["CursorBrake_Edges_DisplayLabel"] = "{0} ({1})",
            ["CursorBrake_Edges_DisplayFallback"] = "Monitor {0}",
            ["CursorBrake_Edges_PairTitle"] = "{0} and {1}",
            ["CursorBrake_Edges_DirectionHeader"] = "From {0} to {1}",
            ["CursorBrake_Edges_WallNote"] = "Wall from {0} to {1}: hold {2} to cross",
            ["CursorBrake_Edges_TrapTitle"] = "Hold {1} to leave {0}",
            ["CursorBrake_Edges_TrapMessage"] = "The cursor enters it from {0} without a key, and every way out is a wall.",
            ["CursorBrake_Edges_Shared"] = "{0} px shared",
            ["CursorBrake_Edges_And"] = "{0} and {1}",
            ["CursorBrake_Edges_LastConnected"] = "Last connected {0}",
            ["CursorBrake_Edges_Kept"] = "Removed after {0}",
            ["CursorBrake_Edges_Description"] = "Give an edge its own strength, turn it off, or make it a wall you cross only by holding {0}. Select an edge on the map or in the list.",
            ["CursorBrake_Edges_WallLegend"] = "Wall: hold {0} to cross",
            ["CursorBrake_Key_Ctrl"] = "Ctrl",
            ["CursorBrake_Key_Shift"] = "Shift",
        };

        private static readonly string[] EdgeLetters = ["A", "B", "C"];
        private static readonly string[] ShowEdgesOnly = ["show_edges"];
        private static readonly bool[] SecondRowOnly = [false, true, false];
        private static readonly bool[] FirstRowOnly = [true, false, false];
        private static readonly string[] LettersAAndB = ["A", "B"];
        private static readonly string[] DescriptionAndLegend = ["EditorDescription", "WallLegend"];

        private static CursorBrakeTextPart Text(string text) => new(text, text);

        private static CursorBrakeTextPart Icon(int number, bool builtIn, string name) => new(name, string.Empty, number, builtIn);

        // Parts compare by what shows: the icon's number and kind, and the text after it.
        private static string DescribeParts(IEnumerable<CursorBrakeTextPart> parts) =>
            string.Join("|", parts.Select(p => p.IsMonitor ? $"[{p.MonitorNumber}{(p.BuiltIn ? "b" : string.Empty)}]{p.Text}" : p.Text));

        private int _saves;
        private List<string> _actions;

        private static string Resource(string key)
        {
            return Strings.TryGetValue(key, out var value) ? value : key;
        }

        private static CursorBrakeEdgeSetting Both(string first, string second, CursorBrakeEdgeMode mode, int pushDistance = 300)
        {
            return new CursorBrakeEdgeSetting { Monitors = { first, second }, Mode = mode, PushDistance = pushDistance };
        }

        private static CursorBrakeEdgeSetting One(string first, string second, CursorBrakeEdgeMode mode, int pushDistance = 300)
        {
            return new CursorBrakeEdgeSetting { Monitors = { first, second }, OneWay = true, Mode = mode, PushDistance = pushDistance };
        }

        private static string Describe(IEnumerable<CursorBrakeEdgeSetting> edges)
        {
            return string.Join("; ", edges.Select(e => $"{string.Join(e.OneWay ? " -> " : " <-> ", e.Monitors)} mode {e.Mode} {e.PushDistance}"));
        }

        private CursorBrakeEdgesViewModel Create(CursorBrakeProperties properties, string detected = Fixture)
        {
            var fileSystem = new MockFileSystem();
            if (detected != null)
            {
                fileSystem.AddFile(DetectedPath, new MockFileData(detected));
            }

            return Create(properties, fileSystem);
        }

        private CursorBrakeEdgesViewModel Create(CursorBrakeProperties properties, MockFileSystem fileSystem)
        {
            _saves = 0;
            _actions = new List<string>();
            var viewModel = new CursorBrakeEdgesViewModel(properties, fileSystem, DetectedPath, () => _saves++, action => _actions.Add(action), Resource);
            viewModel.Reload();
            return viewModel;
        }

        private static CursorBrakeProperties With(params CursorBrakeEdgeSetting[] edges)
        {
            var properties = new CursorBrakeProperties();
            properties.Edges.Value.AddRange(edges);
            return properties;
        }

        [TestMethod]
        public void RowsFollowTheDetectedEdges()
        {
            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Custom, 600), One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));

            Assert.HasCount(3, viewModel.Rows);
            CollectionAssert.AreEqual(EdgeLetters, viewModel.Rows.Select(r => r.Letter).ToArray());
            CollectionAssert.AreEqual(new[] { (0, 1), (0, 2), (1, 2) }, viewModel.Rows.Select(r => (r.First, r.Second)).ToArray());

            var a = viewModel.Rows[0];
            Assert.AreEqual(CustomItem, a.SelectedIndex);
            Assert.AreEqual(600, a.PushDistance);
            Assert.IsTrue(a.IsCustom);
            Assert.IsFalse(a.IsSplit);

            Assert.AreEqual(DefaultItem, viewModel.Rows[1].SelectedIndex);
            Assert.IsFalse(viewModel.Rows[1].IsCustom);
            Assert.AreEqual(0, _saves, "Loading never saves.");
        }

        [TestMethod]
        public void AnEdgeThatDiffersEachWayShowsItsDirections()
        {
            var viewModel = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));

            var c = viewModel.Rows[2];
            Assert.IsTrue(c.IsSplit);
            Assert.AreEqual(DifferentEachWayItem, c.SelectedIndex);
            Assert.AreEqual(DefaultItem, c.Forward.SelectedIndex, "DELL to LENOVO has no entry.");
            Assert.AreEqual(WallItem, c.Backward.SelectedIndex, "LENOVO to DELL is the wall.");
            StringAssert.Contains(c.Description, "Wall from 2 to 3: hold Ctrl to cross", "The row names the wall and the key in its description.");
        }

        [TestMethod]
        [DataRow(CustomItem, CursorBrakeEdgeMode.Custom)]
        [DataRow(OffItem, CursorBrakeEdgeMode.Off)]
        [DataRow(WallItem, CursorBrakeEdgeMode.Wall)]
        public void EachModeWritesOneBothWaysEntry(int item, CursorBrakeEdgeMode mode)
        {
            var properties = With();
            var viewModel = Create(properties);

            viewModel.Rows[0].SelectedIndex = item;

            Assert.AreEqual(Describe([Both(Dell, Hp, mode, 300)]), Describe(properties.Edges.Value));
            Assert.AreEqual(1, _saves);
        }

        [TestMethod]
        public void DefaultDeletesEveryEntryForThePair()
        {
            var properties = With(One(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Dell, Hp, CursorBrakeEdgeMode.Off), Both(Hp, Lenovo, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);

            viewModel.Rows[0].SelectedIndex = DefaultItem;

            Assert.AreEqual(Describe([Both(Hp, Lenovo, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value));
        }

        [TestMethod]
        public void TheSliderWritesTheDistanceAndOffKeepsIt()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Custom, 600));
            var viewModel = Create(properties);

            viewModel.Rows[0].PushDistance = 750;
            Assert.AreEqual(Describe([Both(Dell, Hp, CursorBrakeEdgeMode.Custom, 750)]), Describe(properties.Edges.Value));

            viewModel.Rows[0].SelectedIndex = OffItem;
            viewModel.Rows[0].SelectedIndex = CustomItem;
            Assert.AreEqual(750, viewModel.Rows[0].PushDistance, "Switching back to Custom restores the distance.");
        }

        [TestMethod]
        public void SplittingChangesNothingUntilADirectionChanges()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);
            var a = viewModel.Rows[0];

            a.SelectedIndex = DifferentEachWayItem;
            Assert.IsTrue(a.IsSplit);
            Assert.AreEqual(WallItem, a.Forward.SelectedIndex);
            Assert.AreEqual(WallItem, a.Backward.SelectedIndex);
            Assert.AreEqual(Describe([Both(Hp, Dell, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value));
            Assert.AreEqual(0, _saves);

            a.Backward.SelectedIndex = DefaultItem;
            Assert.AreEqual(Describe([One(Hp, Dell, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value), "One direction Default and the other Wall is a single one-way entry.");

            a.Backward.SelectedIndex = WallItem;
            Assert.AreEqual(Describe([Both(Dell, Hp, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value), "Equal directions collapse into one both-ways entry.");
        }

        [TestMethod]
        public void PickingOneModeOnASplitEdgeJoinsItsDirections()
        {
            var properties = With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);

            viewModel.Rows[2].SelectedIndex = OffItem;

            Assert.IsFalse(viewModel.Rows[2].IsSplit);
            Assert.AreEqual(Describe([Both(Dell, Lenovo, CursorBrakeEdgeMode.Off)]), Describe(properties.Edges.Value));
        }

        [TestMethod]
        public void TheTrapCautionNamesOnlyATrappedDisplay()
        {
            var trapped = Create(With(One(Lenovo, Hp, CursorBrakeEdgeMode.Wall), One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));
            Assert.IsTrue(trapped.IsTrapVisible);
            Assert.AreEqual("Hold Ctrl to leave Lenovo L32p-30 (2)", trapped.TrapTitle);
            Assert.AreEqual("The cursor enters it from 1 and 3 without a key, and every way out is a wall.", trapped.TrapMessage);

            var walledBothWays = Create(With(Both(Lenovo, Hp, CursorBrakeEdgeMode.Wall), Both(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));
            Assert.IsFalse(walledBothWays.IsTrapVisible);
            Assert.AreEqual(string.Empty, walledBothWays.TrapTitle);
        }

        [TestMethod]
        public void PruningDropsEntriesForPairsNotSeenAndSavesOnce()
        {
            var lenovoLg = @"\\?\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4356";
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Lenovo, lenovoLg, CursorBrakeEdgeMode.Wall), One(lenovoLg, Lenovo, CursorBrakeEdgeMode.Off), Both(Hp, Lg, CursorBrakeEdgeMode.Off));

            Create(properties);

            Assert.AreEqual(Describe([Both(Hp, Dell, CursorBrakeEdgeMode.Wall), Both(Hp, Lg, CursorBrakeEdgeMode.Off)]), Describe(properties.Edges.Value));
            Assert.AreEqual(1, _saves);
        }

        [TestMethod]
        public void PruningWaitsForTheModuleToPublishAfterTheSettings()
        {
            // settings.json is newer than the module's file, as right after DSC or Settings wrote an
            // entry the module has not stamped yet.
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(DetectedPath, new MockFileData(Fixture) { LastWriteTime = new System.DateTime(2026, 10, 14, 8, 0, 0, System.DateTimeKind.Utc) });
            fileSystem.AddFile(@"C:\Settings\CursorBrake\settings.json", new MockFileData("{}") { LastWriteTime = new System.DateTime(2026, 10, 14, 9, 0, 0, System.DateTimeKind.Utc) });
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), Both(Lenovo, @"\\?\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4356", CursorBrakeEdgeMode.Wall));

            Create(properties, fileSystem);

            Assert.HasCount(2, properties.Edges.Value);
            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void NothingToPruneSavesNothing()
        {
            Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall)));

            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void AMissingFileShowsTheEmptyStateAndPrunesNothing()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties, detected: null);

            Assert.IsFalse(viewModel.HasDetectedEdges);
            Assert.IsEmpty(viewModel.Rows);
            Assert.HasCount(1, properties.Edges.Value);
            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void ATruncatedFileIsIgnored()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(DetectedPath, new MockFileData(Fixture));
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties, fileSystem);

            fileSystem.File.WriteAllText(DetectedPath, Fixture.Remove(Fixture.Length / 2));
            viewModel.Reload();

            Assert.HasCount(3, viewModel.Rows, "The rows from the last good read stay.");
            Assert.HasCount(1, properties.Edges.Value);
            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void AnUnknownModeRoundTripsUntouched()
        {
            var properties = With(Both(Hp, Lenovo, (CursorBrakeEdgeMode)7), Both(Hp, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);

            viewModel.Rows[0].SelectedIndex = OffItem;

            Assert.AreEqual(Describe([Both(Hp, Lenovo, (CursorBrakeEdgeMode)7), Both(Dell, Hp, CursorBrakeEdgeMode.Off)]), Describe(properties.Edges.Value));
        }

        [TestMethod]
        public void OneDisplayShowsTheConnectAnotherDisplayState()
        {
            const string single = """
                { "version": 1, "identified": true,
                  "monitors": [ { "id": "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "name": "HP E24 G5", "rect": [0, 0, 1920, 1080], "dpi": 96, "primary": true } ],
                  "edges": [], "seen": [] }
                """;
            var viewModel = Create(With(), single);

            Assert.IsTrue(viewModel.IsSingleDisplay);
            Assert.IsEmpty(viewModel.Rows);
        }

        // Each monitor in a row's title shows as its numbered icon and its name, while the plain
        // title keeps the number in text for screen readers.
        [TestMethod]
        public void TitlesShowEachMonitorAsItsIconAndName()
        {
            var a = Create(With()).Rows[0];

            Assert.AreEqual("HP E24 G5 (1) and Dell U2724D (3)", a.Title);
            Assert.AreEqual(DescribeParts(a.TitleParts), DescribeParts([Icon(1, false, "HP E24 G5"), Text(" and "), Icon(3, true, "Dell U2724D")]));
            Assert.AreEqual(a.Title, string.Concat(a.TitleParts.Select(p => p.Plain)), "The parts read as the plain title.");
            Assert.AreEqual(DescribeParts(a.Forward.HeaderParts), DescribeParts([Text("From "), Icon(1, false, "HP E24 G5"), Text(" to "), Icon(3, true, "Dell U2724D")]));
            Assert.AreEqual(a.Forward.Header, string.Concat(a.Forward.HeaderParts.Select(p => p.Plain)));
        }

        // The wall note names its monitors by icon alone, with the number in the plain text.
        [TestMethod]
        public void TheWallNoteShowsMonitorIcons()
        {
            var c = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall))).Rows[2];

            string parts = DescribeParts(c.DescriptionParts);
            StringAssert.Contains(parts, DescribeParts([Text(" · Wall from "), Icon(2, false, string.Empty), Text(" to "), Icon(3, true, string.Empty), Text(": hold Ctrl to cross")]));
            Assert.AreEqual(c.Description, string.Concat(c.DescriptionParts.Select(p => p.Plain)));
        }

        // The wall's key appears in the editor's description and the legend, so both follow the
        // cross key setting.
        // The file stores Custom, Off and Wall as the module's EdgeMode numbers them, and each mode
        // box lists Default, Custom, Off and Wall in that order, with "Different each way" after
        // them on an edge row.
        [TestMethod]
        public void EdgeModesMatchTheFileAndTheBoxOrder()
        {
            Assert.AreEqual(0, (int)CursorBrakeEdgeMode.Custom);
            Assert.AreEqual(1, (int)CursorBrakeEdgeMode.Off);
            Assert.AreEqual(2, (int)CursorBrakeEdgeMode.Wall);

            var modes = new[] { (DefaultItem, CursorBrakeEdgeMode.Default), (CustomItem, CursorBrakeEdgeMode.Custom), (OffItem, CursorBrakeEdgeMode.Off), (WallItem, CursorBrakeEdgeMode.Wall) };
            foreach (var (item, mode) in modes)
            {
                Assert.AreEqual(item, CursorBrakeEdgeChoiceViewModel.ItemOf(mode), $"{mode} is item {item}.");
                Assert.AreEqual(mode, CursorBrakeEdgeChoiceViewModel.ModeOf(item), $"Item {item} is {mode}.");
            }

            Assert.AreEqual(DifferentEachWayItem, CursorBrakeEdgeRowViewModel.DifferentEachWayItem);
            Assert.AreEqual(CursorBrakeEdgeMode.Default, CursorBrakeEdgeChoiceViewModel.ModeOf(DifferentEachWayItem), "Different each way is no mode.");
        }

        // The module's RuleFor gives an edge without an entry the global strength when Apply to
        // brakes its axis and lets the cursor through when it does not, and Show on screens draws
        // it the same way. The editor's Default says which, in its text and on the map.
        [TestMethod]
        [DataRow(CursorBrakeEdgeFilter.All, true, true)]
        [DataRow(CursorBrakeEdgeFilter.SideBySide, true, false)]
        [DataRow(CursorBrakeEdgeFilter.Stacked, false, true)]
        public void DefaultReadsAsApplyToTreatsTheEdge(CursorBrakeEdgeFilter filter, bool sideBySideBrakes, bool stackedBrakes)
        {
            // A (HP | DELL) is side by side; B and C are stacked, and C has a wall from LENOVO to DELL only.
            var properties = With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);

            viewModel.EdgeFilter = filter;

            AssertDefault(viewModel.Rows[0], viewModel.Rows[0], sideBySideBrakes, "A");
            AssertDefault(viewModel.Rows[1], viewModel.Rows[1], stackedBrakes, "B");
            AssertDefault(viewModel.Rows[2], viewModel.Rows[2].Forward, stackedBrakes, "DELL to LENOVO on C");
            Assert.AreEqual(CursorBrakeEdgeMode.Wall, viewModel.Rows[2].Backward.ShownMode, "An entry reads as it is, whatever Apply to says.");
            Assert.AreEqual(Describe([One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value), "Apply to changes no entry.");
            Assert.AreEqual(0, _saves, "Showing what Default does saves nothing.");
        }

        // The page's view model hands Apply to to the editor when it loads and on every change, as
        // it hands over the push distance and the cross key. Settings and detected-edges.json live in
        // a mock file system, and the IPC callback goes nowhere.
        [TestMethod]
        public void ThePageCarriesApplyToIntoTheEditor()
        {
            var fileSystem = new MockFileSystem();
            var settingsUtils = new SettingsUtils(fileSystem);
            var cursorBrake = new CursorBrakeSettings();
            cursorBrake.Properties.EdgeFilter.Value = (int)CursorBrakeEdgeFilter.Stacked;
            var sent = new List<string>();

            var viewModel = new MouseUtilsViewModel(
                settingsUtils,
                Repository(new GeneralSettings()),
                Repository(new AutoHideCursorSettings()),
                Repository(new FindMyMouseSettings()),
                Repository(new MouseHighlighterSettings()),
                Repository(new MouseJumpSettings()),
                Repository(new MousePointerCrosshairsSettings()),
                Repository(new CursorWrapSettings()),
                Repository(new MouseButtonLockSettings()),
                Repository(cursorBrake),
                message =>
                {
                    sent.Add(message);
                    return 0;
                },
                fileSystem);

            Assert.AreEqual(CursorBrakeEdgeFilter.Stacked, viewModel.CursorBrakeEdges.EdgeFilter, "The editor starts from the saved Apply to.");

            viewModel.CursorBrakeEdgeFilter = (int)CursorBrakeEdgeFilter.SideBySide;

            Assert.AreEqual(CursorBrakeEdgeFilter.SideBySide, viewModel.CursorBrakeEdges.EdgeFilter, "The editor follows a change to Apply to.");
            Assert.AreEqual((int)CursorBrakeEdgeFilter.SideBySide, cursorBrake.Properties.EdgeFilter.Value);
        }

        [TestMethod]
        public void AModeTheEnumLacksHasDefaultsPosition()
        {
            Assert.AreEqual(DefaultItem, CursorBrakeEdgeChoiceViewModel.ItemOf((CursorBrakeEdgeMode)7));
            Assert.AreEqual(DefaultItem, CursorBrakeEdgeChoiceViewModel.ItemOf((CursorBrakeEdgeMode)(-2)));
        }

        private static ISettingsRepository<T> Repository<T>(T settings)
            where T : class
        {
            var repository = new Mock<ISettingsRepository<T>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(settings);
            return repository.Object;
        }

        private static void AssertDefault(CursorBrakeEdgeRowViewModel row, CursorBrakeEdgeChoiceViewModel choice, bool brakes, string edge)
        {
            Assert.AreEqual(DefaultItem, choice.SelectedIndex, $"{edge} stays at Default.");
            Assert.AreEqual(brakes ? "Default (300 px)" : "Default (Off)", choice.Items[DefaultItem], $"{edge}'s Default names what it does.");
            Assert.AreEqual(brakes ? "Default (300 px)" : "Default (Off)", row.Items[DefaultItem], $"{edge}'s edge row lists the same Default.");
            Assert.AreEqual(brakes ? CursorBrakeEdgeMode.Default : CursorBrakeEdgeMode.Off, choice.ShownMode, $"The map draws {edge} as the module treats it.");
        }

        // The sliders carry their range as literals, which the XAML parser sets before any binding
        // sets the value, so a bound distance is never clamped into a default range and written back.
        // The literals must stay the range CursorBrakeProperties holds.
        [TestMethod]
        public void EverySliderTakesTheRangeThePropertiesHold()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PowerToys.slnx")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate PowerToys repository root.");
            var document = XDocument.Load(Path.Combine(dir.FullName, @"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseUtilsPage.xaml"));

            // The global slider binds CursorBrakePushDistance; the edge and direction sliders bind PushDistance.
            var sliders = document.Descendants()
                .Where(e => e.Name.LocalName == "Slider" && ((string)e.Attribute("Value") ?? string.Empty).Contains("PushDistance", StringComparison.Ordinal))
                .ToList();

            Assert.HasCount(3, sliders, "The global, edge and direction sliders.");
            foreach (var slider in sliders)
            {
                string name = (string)slider.Attribute(XName.Get("AutomationProperties.AutomationId"));
                Assert.AreEqual(CursorBrakeProperties.MaxPushDistance.ToString(CultureInfo.InvariantCulture), (string)slider.Attribute("Maximum"), $"{name}'s Maximum.");
                Assert.AreEqual(CursorBrakeProperties.MinPushDistance.ToString(CultureInfo.InvariantCulture), (string)slider.Attribute("Minimum"), $"{name}'s Minimum.");
            }
        }

        [TestMethod]
        public void TheDescriptionAndLegendNameTheCrossKey()
        {
            var viewModel = Create(With());
            var changed = new List<string>();
            viewModel.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            Assert.AreEqual("Give an edge its own strength, turn it off, or make it a wall you cross only by holding Ctrl. Select an edge on the map or in the list.", viewModel.EditorDescription);
            Assert.AreEqual("Wall: hold Ctrl to cross", viewModel.WallLegend);

            viewModel.CrossModifier = CursorBrakeCrossModifier.Shift;

            Assert.AreEqual("Give an edge its own strength, turn it off, or make it a wall you cross only by holding Shift. Select an edge on the map or in the list.", viewModel.EditorDescription);
            Assert.AreEqual("Wall: hold Shift to cross", viewModel.WallLegend);
            CollectionAssert.IsSubsetOf(DescriptionAndLegend, changed);
        }

        [TestMethod]
        public void ADormantPairIsListedUntilForgotten()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Hp, Lg, CursorBrakeEdgeMode.Off), One(Lg, Hp, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);

            Assert.HasCount(1, viewModel.DisconnectedRows);
            var dormant = viewModel.DisconnectedRows[0];
            Assert.AreEqual("HP E24 G5 (1) and LGD0A1B", dormant.Title, "A display that is not connected is named by its model code.");
            StringAssert.Contains(dormant.Description, "Last connected");
            string removal = new DateTime(2027, 3, 31).ToString("d", CultureInfo.CurrentCulture);
            StringAssert.Contains(dormant.Description, $"Removed after {removal}", "LG and HP were last seen together on 2026-10-02, so their entries go 180 days later.");

            viewModel.Forget(dormant);

            Assert.IsEmpty(viewModel.DisconnectedRows);
            Assert.AreEqual(Describe([Both(Hp, Dell, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value));
            Assert.AreEqual(1, _saves);
        }

        [TestMethod]
        public void AnUnidentifiedDisplaysEdgesCannotBeEdited()
        {
            var unidentified = Fixture.Replace("\"identified\": true", "\"identified\": false").Replace("\\\\\\\\?\\\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353\", \"name\"", "\", \"name\"");
            var viewModel = Create(With(), unidentified);

            Assert.IsTrue(viewModel.IsUnidentified);
            Assert.IsFalse(viewModel.Rows[0].IsEditable, "HP-DELL");
            Assert.IsTrue(viewModel.Rows[1].IsEditable, "HP-LENOVO");
            Assert.IsFalse(viewModel.Rows[2].IsEditable, "DELL-LENOVO");
        }

        [TestMethod]
        public void TheListShowsDirectionsOnlyForSplitEdges()
        {
            var viewModel = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall), Both(Hp, Lg, CursorBrakeEdgeMode.Off)));
            var a = viewModel.Rows[0];
            var c = viewModel.Rows[2];

            CollectionAssert.AreEqual(new object[] { viewModel, a, viewModel.Rows[1], c, c.Forward, c.Backward, "MouseUtils_CursorBrake_Edges_Disconnected", viewModel.DisconnectedRows[0] }, viewModel.ListItems, "The map first, the split edge with its directions, then the disconnected pair.");

            a.SelectedIndex = DifferentEachWayItem;
            CollectionAssert.AreEqual(new object[] { viewModel, a, a.Forward, a.Backward, viewModel.Rows[1], c, c.Forward, c.Backward, "MouseUtils_CursorBrake_Edges_Disconnected", viewModel.DisconnectedRows[0] }, viewModel.ListItems, "Splitting an edge shows its directions.");

            c.SelectedIndex = WallItem;
            CollectionAssert.AreEqual(new object[] { viewModel, a, a.Forward, a.Backward, viewModel.Rows[1], c, "MouseUtils_CursorBrake_Edges_Disconnected", viewModel.DisconnectedRows[0] }, viewModel.ListItems, "Picking one mode hides them.");
        }

        [TestMethod]
        public void SelectingARowMarksOnlyThatRow()
        {
            var viewModel = Create(With());

            viewModel.Rows[1].Select();

            Assert.AreSame(viewModel.Rows[1], viewModel.SelectedRow);
            CollectionAssert.AreEqual(SecondRowOnly, viewModel.Rows.Select(r => r.IsSelected).ToArray());
        }

        // The list colors a row's letter from IsSelected, so moving the selection must tell both the
        // row it leaves and the row it reaches.
        [TestMethod]
        public void MovingTheSelectionNotifiesBothRows()
        {
            var viewModel = Create(With());
            viewModel.Rows[1].Select();
            var notified = new List<string>();
            foreach (var row in viewModel.Rows)
            {
                row.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CursorBrakeEdgeRowViewModel.IsSelected))
                    {
                        notified.Add(((CursorBrakeEdgeRowViewModel)sender).Letter);
                    }
                };
            }

            viewModel.Rows[0].Select();

            CollectionAssert.AreEquivalent(LettersAAndB, notified);
            CollectionAssert.AreEqual(FirstRowOnly, viewModel.Rows.Select(r => r.IsSelected).ToArray());
        }

        // A settings.json entry the module skips, as DSC or a hand edit writes it, next to two good
        // ones. Loading drops it and the editor shows the rest, without saving.
        [TestMethod]
        [DataRow("null", DisplayName = "a null entry")]
        [DataRow("""{ "monitors": null, "mode": 2 }""", DisplayName = "null monitors")]
        [DataRow("""{ "monitors": [ null, "b" ], "mode": 2 }""", DisplayName = "a null ID")]
        [DataRow("""{ "monitors": [ "a", "b", "c" ], "mode": 2 }""", DisplayName = "three IDs")]
        [DataRow("""{ "monitors": [ "a", "b" ], "mode": "2" }""", DisplayName = "a mode that is a string")]
        public void AnEntryTheModuleSkipsNeverStopsTheEditor(string entry)
        {
            string hp = Hp.Replace(@"\", @"\\");
            string json = $$"""{ "edges": { "value": [ { "monitors": [ "{{hp}}", "{{Dell.Replace(@"\", @"\\")}}" ], "mode": 2 }, {{entry}}, { "monitors": [ "{{hp}}", "{{Lg.Replace(@"\", @"\\")}}" ], "mode": 1 } ] } }""";
            var properties = System.Text.Json.JsonSerializer.Deserialize<CursorBrakeProperties>(json);

            var viewModel = Create(properties);

            Assert.AreEqual(Describe([Both(Hp, Dell, CursorBrakeEdgeMode.Wall), Both(Hp, Lg, CursorBrakeEdgeMode.Off)]), Describe(properties.Edges.Value));
            Assert.AreEqual(WallItem, viewModel.Rows[0].SelectedIndex);
            Assert.HasCount(1, viewModel.DisconnectedRows, "HP and LG are listed as disconnected.");
            Assert.AreEqual(0, _saves, "Loading never saves.");
        }

        // A malformed detected-edges.json, with an entry for a disconnected pair
        // so every list is walked. settings.json is the newer file, so the entry is not pruned. The
        // editor reads the file without failing.
        [TestMethod]
        [DataRow("""{ "version": 1, "identified": true, "monitors": null, "edges": [], "seen": [] }""", DisplayName = "monitors null")]
        [DataRow("""{ "version": 1, "identified": true, "monitors": [ null, null ], "edges": [ { "monitors": [0, 1], "axis": "vertical" } ], "seen": [] }""", DisplayName = "null monitors")]
        [DataRow("""{ "version": 1, "identified": true, "monitors": [], "edges": null, "seen": null }""", DisplayName = "edges and seen null")]
        [DataRow("""{ "version": 1, "identified": true, "monitors": [ { "id": "a", "rect": [0, 0, 1, 1] }, { "id": "b", "rect": [1, 0, 2, 1] } ], "edges": [ { "monitors": null }, null ], "seen": [] }""", DisplayName = "an edge's monitors null")]
        [DataRow("""{ "version": 1, "identified": true, "monitors": [], "edges": [], "seen": [ null, { "monitors": null } ] }""", DisplayName = "seen entries null")]
        public void ADetectedFileWithNullsNeverStopsTheEditor(string detected)
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(DetectedPath, new MockFileData(detected) { LastWriteTime = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc) });
            fileSystem.AddFile(@"C:\Settings\CursorBrake\settings.json", new MockFileData("{}") { LastWriteTime = new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc) });

            var viewModel = Create(With(Both(Hp, Lg, CursorBrakeEdgeMode.Off)), fileSystem);

            Assert.IsNotNull(viewModel.Detected);
            Assert.HasCount(1, viewModel.DisconnectedRows);
        }

        // A hand-edited file can stamp a pair at the end of the calendar, which has no removal
        // date to show.
        [TestMethod]
        public void AStampAtTheEndOfTheCalendarShowsNoRemovalDate()
        {
            string farFuture = Fixture.Replace("\"last_seen_utc\": \"2026-10-02T08:00:00Z\"", "\"last_seen_utc\": \"9999-12-31T00:00:00Z\"");

            var viewModel = Create(With(Both(Hp, Lg, CursorBrakeEdgeMode.Off)), farFuture);

            Assert.HasCount(1, viewModel.DisconnectedRows);
            StringAssert.Contains(viewModel.DisconnectedRows[0].Description, "Last connected");
            Assert.IsFalse(viewModel.DisconnectedRows[0].Description.Contains("Removed after", StringComparison.Ordinal), viewModel.DisconnectedRows[0].Description);
        }

        [TestMethod]
        public void TheEditorShowsTheDistanceTheModuleApplies()
        {
            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Custom, -50), Both(Hp, Lenovo, CursorBrakeEdgeMode.Custom, 5000)));

            Assert.AreEqual(0, viewModel.Rows[0].PushDistance);
            Assert.AreEqual(1000, viewModel.Rows[1].PushDistance);
        }

        // HP (1) is walled on both shared edges, and DELL ends short of the corner HP shares with
        // LENOVO, so the cursor enters HP from LENOVO through that corner and cannot leave.
        [TestMethod]
        public void TheTrapCautionNamesAWayInThroughACorner()
        {
            const string corner = """
                {
                  "version": 1, "identified": true,
                  "monitors": [
                    { "id": "\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "name": "HP E24 G5", "rect": [0, 0, 1920, 1080], "dpi": 96, "primary": true, "number": 1 },
                    { "id": "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353", "name": "Dell U2724D", "rect": [1920, 0, 3840, 800], "dpi": 96, "number": 2 },
                    { "id": "\\\\?\\DISPLAY#LEN66F1#5&1a2b3c4d&0&UID4354", "name": "Lenovo L32p-30", "rect": [1920, 1080, 3840, 2160], "dpi": 96, "number": 3 },
                    { "id": "\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355", "name": "LG 27UL850", "rect": [0, 1080, 1920, 2160], "dpi": 96, "number": 4 }
                  ],
                  "edges": [
                    { "monitors": [0, 1], "axis": "vertical", "at": 1920, "from": 0, "to": 800 },
                    { "monitors": [0, 3], "axis": "horizontal", "at": 1080, "from": 0, "to": 1920 },
                    { "monitors": [2, 3], "axis": "vertical", "at": 1920, "from": 1080, "to": 2160 }
                  ],
                  "seen": [
                    { "monitors": ["\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#DELA1B2#5&1a2b3c4d&0&UID4353"], "last_seen_utc": "2026-10-14T08:00:00Z" },
                    { "monitors": ["\\\\?\\DISPLAY#HWP3A6E#5&1a2b3c4d&0&UID4352", "\\\\?\\DISPLAY#LGD0A1B#5&1a2b3c4d&0&UID4355"], "last_seen_utc": "2026-10-14T08:00:00Z" }
                  ]
                }
                """;

            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), Both(Hp, Lg, CursorBrakeEdgeMode.Wall)), corner);

            Assert.AreEqual("Hold Ctrl to leave HP E24 G5 (1)", viewModel.TrapTitle);
            Assert.AreEqual("The cursor enters it from 3 without a key, and every way out is a wall.", viewModel.TrapMessage);
        }

        [TestMethod]
        public void ShowOnScreensAsksTheModule()
        {
            var viewModel = Create(With());

            viewModel.ShowOnScreens();

            CollectionAssert.AreEqual(ShowEdgesOnly, _actions);
        }

        // The module publishes again after every save, with fresh `seen` stamps and the same
        // monitors. Settings follows the file without replacing the rows a user may be clicking
        // on, and without saving, so a save never feeds another publish.
        [TestMethod]
        public void ARepublishOfTheSameLayoutKeepsEveryRowAndSavesNothing()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(DetectedPath, new MockFileData(Fixture));
            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), Both(Hp, Lg, CursorBrakeEdgeMode.Off)), fileSystem);
            var rows = viewModel.Rows.ToList();
            var disconnected = viewModel.DisconnectedRows.ToList();
            var items = viewModel.ListItems.ToList();

            fileSystem.File.WriteAllText(DetectedPath, Fixture.Replace("2026-10-14T08:00:00Z", "2026-10-14T09:00:00Z", StringComparison.Ordinal));
            viewModel.Reload();

            CollectionAssert.AreEqual(rows, viewModel.Rows.ToList(), "The edge rows are the same objects.");
            CollectionAssert.AreEqual(disconnected, viewModel.DisconnectedRows.ToList(), "The disconnected rows are the same objects.");
            CollectionAssert.AreEqual(items, viewModel.ListItems.ToList(), "The list holds the same items.");
            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void AMovedMonitorRebuildsTheRows()
        {
            var fileSystem = new MockFileSystem();
            fileSystem.AddFile(DetectedPath, new MockFileData(Fixture));
            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall)), fileSystem);
            var first = viewModel.Rows[0];

            fileSystem.File.WriteAllText(DetectedPath, Fixture.Replace("[1920, 0, 4480, 1440]", "[1920, 100, 4480, 1540]", StringComparison.Ordinal));
            viewModel.Reload();

            Assert.AreNotSame(first, viewModel.Rows[0], "A new layout gets new rows.");
            Assert.AreEqual(WallItem, viewModel.Rows[0].SelectedIndex);
        }

        // A ComboBox clears its selection when its items are replaced, as when a list reuses a
        // row's container for another row. That is never a choice, so it must not write Default
        // over the edge's entries.
        [TestMethod]
        public void AComboBoxClearingItsSelectionChangesNothing()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Lenovo, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);
            var before = Describe(properties.Edges.Value);

            viewModel.Rows[0].SelectedIndex = -1;
            viewModel.Rows[2].Backward.SelectedIndex = -1;

            Assert.AreEqual(before, Describe(properties.Edges.Value));
            Assert.AreEqual(WallItem, viewModel.Rows[0].SelectedIndex);
            Assert.AreEqual(WallItem, viewModel.Rows[2].Backward.SelectedIndex);
            Assert.AreEqual(0, _saves);
        }

        [TestMethod]
        public void AnEditThatChangesNoEntrySavesNothing()
        {
            var properties = With(Both(Dell, Hp, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);
            var a = viewModel.Rows[0];

            a.SelectedIndex = DifferentEachWayItem;
            a.SelectedIndex = WallItem;

            Assert.AreEqual(Describe([Both(Dell, Hp, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value));
            Assert.AreEqual(0, _saves, "Joining two Wall directions into Wall writes the entry the settings already hold.");
        }

        // Replacing a ComboBox's items clears its selection, so a refresh that changes no item
        // keeps the list it had.
        [TestMethod]
        public void ARefreshKeepsEachComboBoxsItemsUntilTheyChange()
        {
            var viewModel = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));
            var rowItems = viewModel.Rows[0].Items;
            var directionItems = viewModel.Rows[2].Forward.Items;

            viewModel.CrossModifier = CursorBrakeCrossModifier.Shift;

            Assert.AreSame(rowItems, viewModel.Rows[0].Items);
            Assert.AreSame(directionItems, viewModel.Rows[2].Forward.Items);

            viewModel.GlobalPushDistance = 450;

            Assert.AreEqual("Default (450 px)", viewModel.Rows[0].Items[DefaultItem]);
            Assert.AreEqual("Default (450 px)", viewModel.Rows[2].Forward.Items[DefaultItem]);
        }

        // A two-way SelectedIndex binding as a WinUI ComboBox behaves: replacing its items clears the
        // selection and writes -1 back, and a SelectedIndex change notice sets what it shows.
        private sealed class ComboBinding
        {
            private readonly Func<int> _read;

            public ComboBinding(System.ComponentModel.INotifyPropertyChanged source, Func<int> read, Action<int> write)
            {
                _read = read;
                Shown = read();
                source.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "Items")
                    {
                        Shown = -1;
                        write(-1);
                    }
                    else if (e.PropertyName == "SelectedIndex")
                    {
                        Shown = _read();
                    }
                };
            }

            public int Shown { get; private set; }
        }

        // Strength changes the Default item's text, so every combo box gets new items and clears. The
        // controls must show the values again, or a Wall shows as an empty box.
        [TestMethod]
        public void NewItemsLeaveEachComboBoxShowingItsValue()
        {
            var properties = With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Lenovo, Dell, CursorBrakeEdgeMode.Wall));
            var viewModel = Create(properties);
            var a = viewModel.Rows[0];
            var backward = viewModel.Rows[2].Backward;
            var rowBox = new ComboBinding(a, () => a.SelectedIndex, value => a.SelectedIndex = value);
            var directionBox = new ComboBinding(backward, () => backward.SelectedIndex, value => backward.SelectedIndex = value);

            viewModel.GlobalPushDistance = 450;

            Assert.AreEqual(WallItem, rowBox.Shown, "The edge row's box shows Wall again.");
            Assert.AreEqual(WallItem, directionBox.Shown, "The direction's box shows Wall again.");
            Assert.AreEqual(Describe([Both(Hp, Dell, CursorBrakeEdgeMode.Wall), One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)]), Describe(properties.Edges.Value));
            Assert.AreEqual(0, _saves);
        }

        // The page declares each box's items and binds their text, so the Default item's text must
        // follow Strength and announce the change.
        [TestMethod]
        public void TheDeclaredItemTextFollowsTheStrength()
        {
            var viewModel = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));
            var a = viewModel.Rows[0];
            var forward = viewModel.Rows[2].Forward;
            var changed = new List<string>();
            a.PropertyChanged += (sender, e) => changed.Add("row " + e.PropertyName);
            forward.PropertyChanged += (sender, e) => changed.Add("direction " + e.PropertyName);
            Assert.AreEqual("Default (300 px)", a.DefaultText);
            CollectionAssert.AreEqual(a.Items.ToArray(), new[] { a.DefaultText, a.CustomText, a.OffText, a.WallText, a.DifferentEachWayText }, "Each declared item shows its item's text.");
            CollectionAssert.AreEqual(forward.Items.ToArray(), new[] { forward.DefaultText, forward.CustomText, forward.OffText, forward.WallText });

            viewModel.GlobalPushDistance = 450;

            Assert.AreEqual("Default (450 px)", a.DefaultText);
            Assert.AreEqual("Default (450 px)", forward.DefaultText);
            CollectionAssert.Contains(changed, "row DefaultText");
            CollectionAssert.Contains(changed, "direction DefaultText");
        }

        // A closed ComboBox draws the selected item's text from when it was selected, so the box
        // redraws only when the selection it is given changes.
        private sealed class DrawnText
        {
            private int _index;

            public DrawnText(System.ComponentModel.INotifyPropertyChanged source, Func<int> read, Func<IReadOnlyList<string>> items)
            {
                _index = read();
                Text = items()[_index];
                source.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "SelectedIndex" && read() != _index)
                    {
                        _index = read();
                        Text = _index >= 0 ? items()[_index] : string.Empty;
                    }
                };
            }

            public string Text { get; private set; }
        }

        [TestMethod]
        public void AChangedItemTextIsDrawnAgain()
        {
            var viewModel = Create(With(One(Lenovo, Dell, CursorBrakeEdgeMode.Wall)));
            var b = viewModel.Rows[1];
            var forward = viewModel.Rows[2].Forward;
            var rowBox = new DrawnText(b, () => b.SelectedIndex, () => b.Items);
            var directionBox = new DrawnText(forward, () => forward.SelectedIndex, () => forward.Items);
            Assert.AreEqual("Default (300 px)", rowBox.Text);

            viewModel.GlobalPushDistance = 450;

            Assert.AreEqual("Default (450 px)", rowBox.Text, "The closed edge box draws the new Default text.");
            Assert.AreEqual("Default (450 px)", directionBox.Text, "The closed direction box draws the new Default text.");
            Assert.AreEqual(DefaultItem, b.SelectedIndex);
            Assert.AreEqual(0, _saves);
        }

        // A box that clears itself outside a refresh, as when a list reuses its container, gets the
        // value back rather than staying empty.
        [TestMethod]
        public void AClearedComboBoxGetsItsValueBack()
        {
            var viewModel = Create(With(Both(Hp, Dell, CursorBrakeEdgeMode.Wall)));
            var a = viewModel.Rows[0];
            var shown = a.SelectedIndex;
            a.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(a.SelectedIndex))
                {
                    shown = a.SelectedIndex;
                }
            };

            shown = -1;
            a.SelectedIndex = -1;

            Assert.AreEqual(WallItem, shown);
            Assert.AreEqual(0, _saves);
        }
    }
}
