#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Shikaku.EditorTools
{
    public static class IOSATTPostprocessor
    {
        [PostProcessBuild(250)]
        public static void ConfigureATT(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string projectPath =
                PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string frameworkTarget =
                project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(
                frameworkTarget,
                "AppTrackingTransparency.framework",
                false);
            project.WriteToFile(projectPath);

            string plistPath =
                Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString(
                "NSUserTrackingUsageDescription",
                IOSATTReleaseConfiguration.TrackingUsageDescription);
            plist.WriteToFile(plistPath);

            Debug.Log(
                "Configured AppTrackingTransparency.framework and the " +
                "tracking-purpose description for the iOS export.");
        }
    }
}
#endif
