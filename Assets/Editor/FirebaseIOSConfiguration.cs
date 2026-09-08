#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Shikaku.EditorTools
{
    /// <summary>
    /// Validates the Firebase Apple app configuration before an iOS export.
    /// Firebase identifiers are non-secret, but they must match the exact
    /// bundle and Firebase project used by the shipped app.
    /// </summary>
    public static class FirebaseIOSConfiguration
    {
        public const string AssetPath =
            "Assets/GoogleService-Info.plist";
        public const string ExpectedBundleId =
            "com.smoothbraingames.shikakugo";
        public const string ExpectedProjectId =
            "REPLACE_WITH_SHIKAKU_FIREBASE_PROJECT_ID";
        public const string ExpectedGoogleAppId =
            "REPLACE_WITH_SHIKAKU_FIREBASE_IOS_APP_ID";
        public const string ExpectedSenderId = "REPLACE_WITH_SHIKAKU_FIREBASE_SENDER_ID";
        public const string ExpectedStorageBucket =
            "REPLACE_WITH_SHIKAKU_FIREBASE_STORAGE_BUCKET";

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            string plistPath =
                Path.Combine(Application.dataPath, "GoogleService-Info.plist");

            if (!File.Exists(plistPath))
            {
                errors.Add(
                    $"Firebase iOS configuration is missing at {AssetPath}.");
                return errors;
            }

            try
            {
                var document = new XmlDocument();
                document.Load(plistPath);
                XmlNode dictionary =
                    document.SelectSingleNode("/plist/dict");

                if (dictionary == null)
                {
                    errors.Add(
                        "Firebase iOS configuration does not contain a " +
                        "valid plist dictionary.");
                    return errors;
                }

                var values = ReadDictionary(dictionary);
                AddMismatch(
                    errors,
                    values,
                    "BUNDLE_ID",
                    ExpectedBundleId);
                AddMismatch(
                    errors,
                    values,
                    "PROJECT_ID",
                    ExpectedProjectId);
                AddMismatch(
                    errors,
                    values,
                    "GOOGLE_APP_ID",
                    ExpectedGoogleAppId);
                AddMismatch(
                    errors,
                    values,
                    "GCM_SENDER_ID",
                    ExpectedSenderId);
                AddMismatch(
                    errors,
                    values,
                    "STORAGE_BUCKET",
                    ExpectedStorageBucket);

                if (!values.TryGetValue("API_KEY", out string apiKey) ||
                    string.IsNullOrWhiteSpace(apiKey))
                {
                    errors.Add(
                        "Firebase iOS configuration is missing API_KEY.");
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    "Firebase iOS configuration could not be read: " +
                    exception.Message);
            }

            return errors;
        }

        private static Dictionary<string, string> ReadDictionary(
            XmlNode dictionary)
        {
            var values =
                new Dictionary<string, string>(StringComparer.Ordinal);

            for (XmlNode node = dictionary.FirstChild;
                 node != null;
                 node = node.NextSibling)
            {
                if (node.NodeType != XmlNodeType.Element ||
                    !string.Equals(
                        node.Name,
                        "key",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                XmlNode valueNode = NextElement(node.NextSibling);
                if (valueNode == null)
                    continue;

                string value =
                    string.Equals(
                        valueNode.Name,
                        "true",
                        StringComparison.Ordinal)
                        ? bool.TrueString
                        : string.Equals(
                            valueNode.Name,
                            "false",
                            StringComparison.Ordinal)
                            ? bool.FalseString
                            : valueNode.InnerText;

                values[node.InnerText] = value;
            }

            return values;
        }

        private static XmlNode NextElement(XmlNode node)
        {
            while (node != null &&
                   node.NodeType != XmlNodeType.Element)
            {
                node = node.NextSibling;
            }

            return node;
        }

        private static void AddMismatch(
            ICollection<string> errors,
            IReadOnlyDictionary<string, string> values,
            string key,
            string expected)
        {
            if (!values.TryGetValue(key, out string actual))
            {
                errors.Add(
                    $"Firebase iOS configuration is missing {key}.");
                return;
            }

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                errors.Add(
                    $"Firebase iOS {key} must be '{expected}' " +
                    $"(currently '{actual}').");
            }
        }
    }
}
#endif
