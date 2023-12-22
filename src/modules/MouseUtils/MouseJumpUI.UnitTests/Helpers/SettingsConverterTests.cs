// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using Microsoft.MouseJumpUI.UnitTests.TestUtils;
using Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseJumpUI.UnitTests.Models.Settings.V2_0;

[TestClass]
public static class SettingsConverterTests
{
    [TestClass]
    public sealed class ParseAppSettingsTests
    {
        public sealed class TestCase
        {
            public TestCase(string settingsJson, MouseJumpSettings expectedResult)
            {
                this.SettingsJson = settingsJson;
                this.ExpectedResult = expectedResult;
            }

            public string SettingsJson { get; }

            public MouseJumpSettings ExpectedResult { get; }
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            /* color conversion */
            yield return new object[]
            {
                new TestCase(
                    settingsJson: SerializationUtils.SerializeAnonymousType(
                        new
                        {
                            version = "2.0",
                            properties = new
                            {
                                preview = new
                                {
                                    canvas = new
                                    {
                                        border = new
                                        {
                                            color = "SystemColors.Highlight",
                                        },
                                        background = new
                                        {
                                            color1 = "#0D57D2",
                                            color2 = "#0344C0",
                                        },
                                    },
                                    screens = new
                                    {
                                        border = new
                                        {
                                            color = "#222222",
                                        },
                                        background = new
                                        {
                                            color1 = "Color.MidnightBlue",
                                            color2 = "Color.MidnightBlue",
                                        },
                                    },
                                },
                            },
                        }),
                    expectedResult: new(
                        version: "2.0",
                        properties: new(
                            activationShortcut: null,
                            previewStyle: new(
                                canvasSize: null,
                                canvasStyle: new(
                                    borderStyle: new(
                                        color: SystemColors.Highlight,
                                        width: null,
                                        depth: null
                                    ),
                                    paddingStyle: null,
                                    backgroundStyle: new(
                                        color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                                        color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
                                    )
                                ),
                                screenStyle: new(
                                    marginStyle: null,
                                    borderStyle: new(
                                        color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                                        width: null,
                                        depth: null
                                    ),
                                    backgroundStyle: new(
                                        color1: Color.MidnightBlue,
                                        color2: Color.MidnightBlue
                                    )
                                )
                            )
                        )
                    )),
            };
            /* all properties specified */
            yield return new object[]
            {
                new TestCase(
                    settingsJson: SerializationUtils.SerializeAnonymousType(
                        new
                        {
                            version = "2.0",
                            properties = new
                            {
                                activation_shortcut = new
                                {
                                    win = true,
                                    ctrl = false,
                                    alt = false,
                                    shift = true,
                                    code = 68,
                                },
                                preview = new
                                {
                                    size = new
                                    {
                                        width = 800,
                                        height = 600,
                                    },
                                    canvas = new
                                    {
                                        border = new
                                        {
                                            color = "SystemColors.Highlight",
                                            width = 6,
                                            depth = 0,
                                        },
                                        padding = new
                                        {
                                            width = 4,
                                        },
                                        background = new
                                        {
                                            color1 = "#0D57D2",
                                            color2 = "#0344C0",
                                        },
                                    },
                                    screens = new
                                    {
                                        margin = new
                                        {
                                            width = 2,
                                        },
                                        border = new
                                        {
                                            color = "#222222",
                                            width = 10,
                                            depth = 3,
                                        },
                                        background = new
                                        {
                                            color1 = "Color.MidnightBlue",
                                            color2 = "Color.MidnightBlue",
                                        },
                                    },
                                },
                            },
                        }),
                    expectedResult: new(
                        version: "2.0",
                        properties: new(
                            activationShortcut: new(true, false, false, true, 68),
                            previewStyle: new(
                                canvasSize: new(800, 600),
                                canvasStyle: new(
                                    borderStyle: new(
                                        color: SystemColors.Highlight,
                                        width: 6,
                                        depth: 0
                                    ),
                                    paddingStyle: new(
                                        width: 4
                                    ),
                                    backgroundStyle: new(
                                        color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                                        color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
                                    )
                                ),
                                screenStyle: new(
                                    marginStyle: new(
                                        width: 2
                                    ),
                                    borderStyle: new(
                                        color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                                        width: 10,
                                        depth: 3
                                    ),
                                    backgroundStyle: new(
                                        color1: Color.MidnightBlue,
                                        color2: Color.MidnightBlue
                                    )
                                )
                            )
                        )
                    )),
            };
        }

        [TestMethod]
        [DynamicData(nameof(GetTestCases), DynamicDataSourceType.Method)]
        public void RunTestCases(TestCase data)
        {
            var actual = JsonSerializer.Deserialize<MouseJumpSettings>(data.SettingsJson)
                ?? throw new InvalidOperationException();
            var expected = data.ExpectedResult;
            Console.WriteLine(actual.ToJsonString());
            Console.WriteLine(expected.ToJsonString());
            Assert.AreEqual(expected.ToJsonString(), actual.ToJsonString());
        }
    }
}
