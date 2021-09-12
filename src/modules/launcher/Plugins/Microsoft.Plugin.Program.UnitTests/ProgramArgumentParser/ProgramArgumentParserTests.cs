// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Program.ProgramArgumentParser;
using Mono.Collections.Generic;
using NUnit.Framework;
using Wox.Plugin;

namespace Microsoft.Plugin.Program.UnitTests.ProgramArgumentParser
{
    [TestFixture]
    public class ProgramArgumentParserTests
    {
        [TestCase("Microsoft Edge", "Microsoft Edge", null)]
        [TestCase("Microsoft Edge ---inprivate", "Microsoft Edge ---inprivate", null)]
        [TestCase("Microsoft Edge -- -inprivate", "Microsoft Edge", "-inprivate")]
        [TestCase("Microsoft Edge -inprivate", "Microsoft Edge", "-inprivate")]
        [TestCase("Microsoft Edge /inprivate", "Microsoft Edge", "/inprivate")]
        [TestCase("edge.exe --inprivate", "edge.exe", "--inprivate")]
        [TestCase("edge.exe -- --inprivate", "edge.exe", "--inprivate")]
        [TestCase("edge.exe", "edge.exe", null)]
        [TestCase("edge", "edge", null)]
        [TestCase("cmd /c \"ping 1.1.1.1\"", "cmd", "/c \"ping 1.1.1.1\"")]
        public void ProgramArgumentParserTestsCanParseQuery(string inputQuery, string expectedProgram, string expectedProgramArguments)
        {
            // Arrange
            var argumentParsers = new IProgramArgumentParser[]
           {
                new DoubleDashProgramArgumentParser(),
                new InferredProgramArgumentParser(),
                new NoArgumentsArgumentParser(),
           };

            var query = new Query(inputQuery);

            // Act
            string program = null, programArguments = null;
            foreach (var argumentParser in argumentParsers)
            {
                if (argumentParser.TryParse(query, out program, out programArguments))
                {
                    break;
                }
            }

            // Assert
            Assert.AreEqual(expectedProgram, program);
            Assert.AreEqual(expectedProgramArguments, programArguments);
        }
    }
}
