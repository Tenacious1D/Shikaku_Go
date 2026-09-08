#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Blocks invalid iOS exports and installs the app-owned privacy manifest
    /// in the main target's Copy Bundle Resources phase.
    /// </summary>
    public sealed class IOSPrivacyManifestBuildProcessor :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -850;

        [MenuItem(
            "Tools/Shikaku Go/iOS/Validate Privacy Manifest")]
        public static void ValidateFromMenu()
        {
            IReadOnlyList<string> errors =
                IOSPrivacyManifestConfiguration.CollectErrors();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "iOS Privacy Manifest",
                    "The app privacy manifest and bundled Unity SDK " +
                    "manifests are valid.",
                    "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "iOS Privacy Manifest Needs Attention",
                "- " + string.Join("\n- ", errors),
                "OK");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors =
                IOSPrivacyManifestConfiguration.CollectErrors();
            if (errors.Count == 0)
                return;

            throw new BuildFailedException(
                "iOS privacy manifest validation failed:\n- " +
                string.Join("\n- ", errors));
        }

        [PostProcessBuild(1000)]
        public static void AddPrivacyManifest(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            IReadOnlyList<string> errors =
                IOSPrivacyManifestConfiguration.CollectErrors();
            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    "iOS privacy manifest validation failed:\n- " +
                    string.Join("\n- ", errors));
            }

            ApplyToExport(buildPath);
            Debug.Log(
                "Added the validated PrivacyInfo.xcprivacy to the " +
                "exported iOS application target.");
        }

        internal static void ApplyToExport(string buildPath)
        {
            if (string.IsNullOrWhiteSpace(buildPath))
            {
                throw new ArgumentException(
                    "An iOS export path is required.",
                    nameof(buildPath));
            }

            string absoluteBuildPath = Path.GetFullPath(buildPath);
            if (!Directory.Exists(absoluteBuildPath))
            {
                throw new DirectoryNotFoundException(
                    $"iOS export directory not found: {absoluteBuildPath}");
            }

            string destinationPath = CopyManifest(absoluteBuildPath);
            WireManifestIntoMainTarget(absoluteBuildPath);
            ValidateExportedManifest(destinationPath);
        }

        internal static string CopyManifest(string buildPath)
        {
            string destinationPath = Path.Combine(
                Path.GetFullPath(buildPath),
                IOSPrivacyManifestConfiguration.ExportFileName);
            File.Copy(
                IOSPrivacyManifestConfiguration.AbsoluteManifestPath,
                destinationPath,
                true);
            return destinationPath;
        }

        private static void WireManifestIntoMainTarget(
            string buildPath)
        {
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException(
                    "The exported Xcode project could not be found.",
                    projectPath);
            }

            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string projectFilePath =
                IOSPrivacyManifestConfiguration.ExportFileName;
            string fileGuid = project.FindFileGuidByProjectPath(
                projectFilePath);
            if (string.IsNullOrEmpty(fileGuid))
            {
                fileGuid = project.AddFile(
                    projectFilePath,
                    projectFilePath,
                    PBXSourceTree.Source);
            }
            else
            {
                project.RemoveFileFromBuild(
                    project.GetUnityMainTargetGuid(),
                    fileGuid);
            }

            project.AddFileToBuild(
                project.GetUnityMainTargetGuid(),
                fileGuid);
            project.WriteToFile(projectPath);
        }

        private static void ValidateExportedManifest(
            string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                throw new BuildFailedException(
                    "The exported iOS privacy manifest is missing.");
            }

            var manifest = new PlistDocument();
            manifest.ReadFromFile(destinationPath);
            var errors = new List<string>();
            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);
            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    "The exported iOS privacy manifest is invalid:\n- " +
                    string.Join("\n- ", errors));
            }
        }
    }
}
#endif
