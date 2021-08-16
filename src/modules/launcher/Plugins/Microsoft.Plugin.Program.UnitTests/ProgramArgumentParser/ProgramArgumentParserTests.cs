// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Program.ProgramArgumentParser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Microsoft.Plugin.Program.UnitTests.ProgramArgumentParser
{
    [TestClass]
    public class ProgramArgumentParserTests
    {
        [DataTestMethod]
        [DataRow("Microsoft Edge", "Microsoft Edge", null)]
        [DataRow("Microsoft Edge ---inprivate", "Microsoft Edge ---inprivate", null)]
        [DataRow("Microsoft Edge -- -inprivate", "Microsoft Edge", "-inprivate")]
        [DataRow("Microsoft Edge -inprivate", "Microsoft Edge", "-inprivate")]
        [DataRow("Microsoft Edge /inprivate", "Microsoft Edge", "/inprivate")]
        [DataRow("edge.exe --inprivate", "edge.exe", "--inprivate")]
        [DataRow("edge.exe -- --inprivate", "edge.exe", "--inprivate")]
        [DataRow("edge.exe", "edge.exe", null)]
        [DataRow("edge", "edge", null)]
        [DataRow("cmd /c \"ping 1.1.1.1\"", "cmd", "/c \"ping 1.1.1.1\"")]
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
