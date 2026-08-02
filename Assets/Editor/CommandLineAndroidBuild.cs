using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CommandLineAndroidBuild
{
    private const string DefaultEnvironment = "pico";

    private sealed class BuildEnvironmentConfiguration
    {
        public BuildEnvironmentConfiguration(string name, string productName)
        {
            Name = name;
            ProductName = productName;
        }

        public string Name { get; }
        public string ProductName { get; }
    }

    public static void BuildDebug()
    {
        BuildEnvironmentConfiguration environment = ResolveBuildEnvironment();
        string outputPath = GetArgument("-outputPath") ?? "Builds/xr_vlc-debug.apk";
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string originalProductName = PlayerSettings.productName;
        PlayerSettings.productName = environment.ProductName;

        try
        {
            Console.WriteLine(
                $"Android build environment: {environment.Name}, product name: {environment.ProductName}");

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
            }

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development |
                          BuildOptions.AllowDebugging |
                          BuildOptions.DetailedBuildReport,
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android debug build failed: {report.summary.result}");
            }

            Console.WriteLine($"Android debug build succeeded: {report.summary.outputPath}");
        }
        finally
        {
            PlayerSettings.productName = originalProductName;
        }
    }

    private static BuildEnvironmentConfiguration ResolveBuildEnvironment()
    {
        string environmentName = GetArgument("-env") ?? DefaultEnvironment;
        switch (environmentName.Trim().ToLowerInvariant())
        {
            case "pico":
                return new BuildEnvironmentConfiguration("pico", "VLC for PICO");
            default:
                throw new ArgumentException(
                    $"Unsupported build environment '{environmentName}'. Supported environments: pico.");
        }
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name)
            {
                if (i == args.Length - 1 || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Missing value for command-line argument '{name}'.");
                }

                return args[i + 1];
            }
        }

        return null;
    }
}
