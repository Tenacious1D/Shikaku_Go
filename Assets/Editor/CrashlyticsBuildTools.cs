using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Ensures Android IL2CPP builds generate public native symbols and provides
/// a Unity menu command for uploading the newest package to Crashlytics.
/// </summary>
public sealed class CrashlyticsBuildTools :
    IPreprocessBuildWithReport,
    IPostprocessBuildWithReport
{
    private const string GoogleServicesPath =
        "Assets/google-services.json";
    private const string LatestSymbolsEditorPref =
        "ShikakuGo.Crashlytics.LatestSymbolsPath";
    private const string UploadMenuPath =
        "Tools/Shikaku Go/Crashlytics/Upload Latest Android Symbols";

    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsAndroidIl2CppBuild(report))
            return;

        EditorUserBuildSettings.androidCreateSymbols =
            AndroidCreateSymbols.Public;

        Debug.Log(
            "Crashlytics: public Android IL2CPP symbol generation is enabled.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsAndroidIl2CppBuild(report))
            return;

        string symbolsPath = FindNewestSymbolsPackage(
            report.summary.outputPath);

        if (string.IsNullOrEmpty(symbolsPath))
        {
            Debug.LogWarning(
                "Crashlytics: the Android build completed, but no " +
                "*.symbols.zip package was found. Confirm the build used " +
                "IL2CPP and Create symbols.zip is set to Public.");
            return;
        }

        EditorPrefs.SetString(LatestSymbolsEditorPref, symbolsPath);
        Debug.Log(
            "Crashlytics symbols are ready: " + symbolsPath + "\n" +
            "Upload them with " + UploadMenuPath + ".");
    }

    [MenuItem(UploadMenuPath)]
    private static void UploadLatestAndroidSymbols()
    {
        string symbolsPath = GetLatestSymbolsPath();
        if (string.IsNullOrEmpty(symbolsPath))
        {
            EditorUtility.DisplayDialog(
                "Crashlytics Symbols Not Found",
                "Build an Android IL2CPP APK or AAB first. Unity will " +
                "generate and remember its *.symbols.zip package.",
                "OK");
            return;
        }

        string firebaseAppId = ReadFirebaseAndroidAppId();
        if (string.IsNullOrEmpty(firebaseAppId))
        {
            EditorUtility.DisplayDialog(
                "Firebase App ID Not Found",
                "Could not read mobilesdk_app_id from " +
                GoogleServicesPath + ".",
                "OK");
            return;
        }

        bool upload = EditorUtility.DisplayDialog(
            "Upload Crashlytics Symbols?",
            "Upload this Android symbol package to Firebase?\n\n" +
            symbolsPath,
            "Upload",
            "Cancel");

        if (!upload)
            return;

        UploadSymbols(firebaseAppId, symbolsPath);
    }

    [MenuItem(UploadMenuPath, true)]
    private static bool ValidateUploadLatestAndroidSymbols()
    {
        return !EditorApplication.isCompiling;
    }

    private static bool IsAndroidIl2CppBuild(BuildReport report)
    {
        return report.summary.platform == BuildTarget.Android &&
            PlayerSettings.GetScriptingBackend(
                NamedBuildTarget.Android) ==
            ScriptingImplementation.IL2CPP;
    }

    private static string GetLatestSymbolsPath()
    {
        string remembered = EditorPrefs.GetString(
            LatestSymbolsEditorPref,
            string.Empty);

        if (File.Exists(remembered))
            return Path.GetFullPath(remembered);

        return FindNewestSymbolsPackage(string.Empty);
    }

    private static string FindNewestSymbolsPackage(string buildOutputPath)
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));

        string outputDirectory = string.IsNullOrEmpty(buildOutputPath)
            ? projectRoot
            : Path.GetDirectoryName(Path.GetFullPath(buildOutputPath));

        string[] searchDirectories =
        {
            outputDirectory,
            projectRoot
        };

        return searchDirectories
            .Where(directory => !string.IsNullOrEmpty(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .SelectMany(directory =>
                Directory.GetFiles(
                    directory,
                    "*.symbols.zip",
                    SearchOption.TopDirectoryOnly))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string ReadFirebaseAndroidAppId()
    {
        if (!File.Exists(GoogleServicesPath))
            return string.Empty;

        string json = File.ReadAllText(GoogleServicesPath);
        Match match = Regex.Match(
            json,
            "\"mobilesdk_app_id\"\\s*:\\s*\"([^\"]+)\"");

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static void UploadSymbols(
        string firebaseAppId,
        string symbolsPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
#if UNITY_EDITOR_WIN
                FileName = "cmd.exe",
                Arguments =
                    "/c firebase crashlytics:symbols:upload --app=\"" +
                    firebaseAppId + "\" \"" + symbolsPath + "\"",
#else
                FileName = "/bin/bash",
                Arguments =
                    "-lc \"firebase crashlytics:symbols:upload --app='" +
                    firebaseAppId + "' '" + symbolsPath + "'\"",
#endif
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException(
                    "Firebase CLI process could not be started.");

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.LogError(
                    "Crashlytics symbol upload failed.\n" +
                    output + "\n" + error);
                EditorUtility.DisplayDialog(
                    "Crashlytics Upload Failed",
                    "Firebase CLI could not upload the symbols. Make " +
                    "sure it is installed and run 'firebase login', then " +
                    "try the menu command again. See the Console for details.",
                    "OK");
                return;
            }

            Debug.Log(
                "Crashlytics symbol upload completed.\n" + output);
            EditorUtility.DisplayDialog(
                "Crashlytics Upload Complete",
                "The Android symbols were uploaded successfully.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Firebase CLI Not Available",
                "Install the Firebase CLI and run 'firebase login', then " +
                "try the upload command again.",
                "OK");
        }
    }
}
