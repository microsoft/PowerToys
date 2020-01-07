#tool nuget:?package=xunit.runner.console&version=2.4.1

var target = Argument<string>("target");
var configuration = Argument<string>("configuration");

var platforms = new[] { "x64", "x86" };

Task("Restore")
    .Does(
        () =>
            NuGetRestore("ImageResizer.sln"));

Task("Build")
    .IsDependentOn("Restore")
    .Does(
        () =>
        {
            foreach (var platform in platforms)
            {
                MSBuild(
                    "ImageResizer.sln",
                    new MSBuildSettings
                    {
                        ArgumentCustomization = args => args.Append("/nologo")
                    }
                        .SetConfiguration(configuration)
                        .SetMaxCpuCount(0)
                        .SetVerbosity(Verbosity.Minimal)
                        .WithProperty("Platform", platform));
            }
        });

Task("Clean")
    .Does(
        () =>
        {
            foreach (var platform in platforms)
            {
                MSBuild(
                    "ImageResizer.sln",
                    new MSBuildSettings()
                        .SetConfiguration(configuration)
                        .WithProperty("Platform", platform)
                        .WithTarget("Clean"));
            }
        });

Task("Test")
    .IsDependentOn("Build")
    .Does(
        () =>
            XUnit2(
                "./test/ImageResizer.Test/bin/" + configuration + "/ImageResizer.Test.dll",
                new XUnit2Settings
                {
                    NoAppDomain = true,
                    ArgumentCustomization = args => args.Append("-nologo")
                }));

RunTarget(target);
