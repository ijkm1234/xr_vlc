using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CommandLineAndroidBuild
{
    private const string DefaultEnvironment = "pico";
    private const string KeystorePathVariable = "XRVLC_KEYSTORE_PATH";
    private const string KeystorePasswordVariable = "XRVLC_KEYSTORE_PASS";
    private const string KeyAliasVariable = "XRVLC_KEY_ALIAS";
    private const string KeyAliasPasswordVariable = "XRVLC_KEY_ALIAS_PASS";

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
        EnsureOutputDirectory(outputPath);
        EnsureVlcAarVariant("debug");

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

            string[] scenes = GetEnabledScenes();

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

    public static void BuildRelease()
    {
        BuildEnvironmentConfiguration environment = ResolveBuildEnvironment();
        string outputPath = GetArgument("-outputPath") ?? "Builds/xr_vlc-release.apk";
        string outputExtension = Path.GetExtension(outputPath);
        bool buildAppBundle;
        if (string.Equals(outputExtension, ".apk", StringComparison.OrdinalIgnoreCase))
        {
            buildAppBundle = false;
        }
        else if (string.Equals(outputExtension, ".aab", StringComparison.OrdinalIgnoreCase))
        {
            buildAppBundle = true;
        }
        else
        {
            throw new ArgumentException("Release output path must end with .apk or .aab.");
        }

        string keystorePath = GetRequiredEnvironmentVariable(KeystorePathVariable);
        if (!Path.IsPathRooted(keystorePath))
        {
            throw new ArgumentException($"{KeystorePathVariable} must be an absolute path.");
        }

        keystorePath = Path.GetFullPath(keystorePath);
        if (!File.Exists(keystorePath))
        {
            throw new FileNotFoundException(
                $"Android release keystore not found at {keystorePath}.",
                keystorePath);
        }

        string keystorePassword = GetRequiredEnvironmentVariable(KeystorePasswordVariable);
        string keyAlias = GetRequiredEnvironmentVariable(KeyAliasVariable);
        string keyAliasPassword = GetRequiredEnvironmentVariable(KeyAliasPasswordVariable);

        EnsureOutputDirectory(outputPath);
        EnsureVlcAarVariant("release");

        string originalProductName = PlayerSettings.productName;
        bool originalUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        string originalKeystoreName = PlayerSettings.Android.keystoreName;
        string originalKeystorePassword = PlayerSettings.Android.keystorePass;
        string originalKeyAliasName = PlayerSettings.Android.keyaliasName;
        string originalKeyAliasPassword = PlayerSettings.Android.keyaliasPass;
        bool originalDevelopment = EditorUserBuildSettings.development;
        bool originalConnectProfiler = EditorUserBuildSettings.connectProfiler;
        bool originalBuildAppBundle = EditorUserBuildSettings.buildAppBundle;

        try
        {
            PlayerSettings.productName = environment.ProductName;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyAliasPassword;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            Console.WriteLine(
                $"Android release build environment: {environment.Name}, " +
                $"product name: {environment.ProductName}, format: {(buildAppBundle ? "AAB" : "APK")}, " +
                $"keystore: {keystorePath}, alias: {keyAlias}");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.DetailedBuildReport,
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android release build failed: {report.summary.result}");
            }

            Console.WriteLine($"Android release build succeeded: {report.summary.outputPath}");
        }
        finally
        {
            PlayerSettings.productName = originalProductName;
            PlayerSettings.Android.useCustomKeystore = originalUseCustomKeystore;
            PlayerSettings.Android.keystoreName = originalKeystoreName;
            PlayerSettings.Android.keystorePass = originalKeystorePassword;
            PlayerSettings.Android.keyaliasName = originalKeyAliasName;
            PlayerSettings.Android.keyaliasPass = originalKeyAliasPassword;
            EditorUserBuildSettings.development = originalDevelopment;
            EditorUserBuildSettings.connectProfiler = originalConnectProfiler;
            EditorUserBuildSettings.buildAppBundle = originalBuildAppBundle;
        }
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
    }

    private static void EnsureVlcAarVariant(string variant)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string requiredAar = Path.Combine(
            projectRoot,
            "Assets",
            "Plugins",
            "Android",
            $"vlc-android-{variant}.aar");
        string otherVariant = variant == "debug" ? "release" : "debug";
        string conflictingAar = Path.Combine(
            projectRoot,
            "Assets",
            "Plugins",
            "Android",
            $"vlc-android-{otherVariant}.aar");

        if (!File.Exists(requiredAar))
        {
            string buildCommand = variant == "debug"
                ? "./scripts/build-vlc-android-aar.sh"
                : "VLC_ANDROID_AAR_VARIANT=release ./scripts/build-vlc-android-aar.sh";
            throw new FileNotFoundException(
                $"Required VLC Android {variant} AAR is missing. Run: {buildCommand}",
                requiredAar);
        }

        if (File.Exists(conflictingAar))
        {
            throw new InvalidOperationException(
                $"Conflicting VLC Android {otherVariant} AAR is present at {conflictingAar}. " +
                "Re-run the AAR build script for the requested variant so only one VLC AAR is installed.");
        }
    }

    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
        }

        return scenes;
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required environment variable {name} is not set.");
        }

        return value;
    }

    private static BuildEnvironmentConfiguration ResolveBuildEnvironment()
    {
        string environmentName = GetArgument("-env") ?? DefaultEnvironment;
        switch (environmentName.Trim().ToLowerInvariant())
        {
            case "pico":
                return new BuildEnvironmentConfiguration("pico", "XRVLC for PICO");
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
