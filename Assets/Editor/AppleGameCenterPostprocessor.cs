#if UNITY_EDITOR && UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Adds Game Center to every exported iOS Xcode project. This keeps the
    /// entitlement and framework repeatable instead of relying on a manual
    /// Xcode toggle.
    /// </summary>
    public static class AppleGameCenterPostprocessor
    {
        private const string EntitlementsFile =
            "ShikakuGo.entitlements";

        [PostProcessBuild(100)]
        public static void AddGameCenterCapability(
            BuildTarget target,
            string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            var capabilities = new ProjectCapabilityManager(
                projectPath,
                EntitlementsFile,
                null,
                mainTargetGuid);

            capabilities.AddGameCenter();
            capabilities.WriteToFile();

            Debug.Log(
                "Added the Apple Game Center capability and entitlement " +
                "to the exported iOS project.");
        }
    }
}
#endif
