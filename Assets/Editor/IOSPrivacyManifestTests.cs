#if UNITY_EDITOR && UNITY_IOS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

namespace Shikaku.EditorTools.Tests
{
    public sealed class IOSPrivacyManifestTests
    {
        [Test]
        public void ProductionManifestPassesValidation()
        {
            IReadOnlyList<string> errors =
                IOSPrivacyManifestConfiguration.CollectErrors();

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ManifestOmitsAppOwnedTrackingAndDeclaresAggregateReasons()
        {
            var manifest = new PlistDocument();
            manifest.ReadFromFile(
                IOSPrivacyManifestConfiguration.AbsoluteManifestPath);

            Assert.That(
                manifest.root.values.ContainsKey("NSPrivacyTracking"),
                Is.False);
            Assert.That(
                manifest.root.values.ContainsKey(
                    "NSPrivacyTrackingDomains"),
                Is.False);

            string contents = File.ReadAllText(
                IOSPrivacyManifestConfiguration.AbsoluteManifestPath);
            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.UserDefaultsCategory));
            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.UserDefaultsReason));            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.FileTimestampCategory));
            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.FileTimestampReason));
            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.DiskSpaceCategory));
            Assert.That(
                contents,
                Does.Contain(
                    IOSPrivacyManifestConfiguration.DiskSpaceReason));
        }

        [Test]
        public void ValidationRejectsMissingRequiredReasonApis()
        {
            var manifest = new PlistDocument();
            manifest.ReadFromFile(
                IOSPrivacyManifestConfiguration.AbsoluteManifestPath);
            manifest.root.values.Remove("NSPrivacyAccessedAPITypes");
            var errors = new List<string>();

            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);

            Assert.That(
                errors,
                Has.Some.Contains("NSPrivacyAccessedAPITypes"));
        }

        [Test]
        public void ValidationRejectsTrackingWithoutDomains()
        {
            var manifest = LoadProductionManifest();
            manifest.root.SetBoolean("NSPrivacyTracking", true);
            var errors = new List<string>();

            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);

            Assert.That(errors, Has.Some.Contains("non-empty"));
        }

        [Test]
        public void ValidationRejectsTrackingWithEmptyDomains()
        {
            var manifest = LoadProductionManifest();
            manifest.root.SetBoolean("NSPrivacyTracking", true);
            manifest.root.CreateArray("NSPrivacyTrackingDomains");
            var errors = new List<string>();

            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);

            Assert.That(errors, Has.Some.Contains("non-empty"));
        }

        [Test]
        public void ValidationRejectsDomainsWhenTrackingIsFalse()
        {
            var manifest = LoadProductionManifest();
            manifest.root.SetBoolean("NSPrivacyTracking", false);
            manifest.root
                .CreateArray("NSPrivacyTrackingDomains")
                .AddString("tracking.example.com");
            var errors = new List<string>();

            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);

            Assert.That(errors, Has.Some.Contains("must omit"));
        }

        [Test]
        public void ValidationAcceptsTrackingWithAValidDomain()
        {
            var manifest = LoadProductionManifest();
            manifest.root.SetBoolean("NSPrivacyTracking", true);
            manifest.root
                .CreateArray("NSPrivacyTrackingDomains")
                .AddString("tracking.example.com");
            var errors = new List<string>();

            IOSPrivacyManifestConfiguration.ValidateDocument(
                manifest,
                errors);

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void CopyManifestProducesIdenticalExportResource()
        {
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "ShikakuPrivacyManifestTests",
                Path.GetRandomFileName());
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                string exportedPath =
                    IOSPrivacyManifestBuildProcessor.CopyManifest(
                        temporaryDirectory);

                Assert.That(File.Exists(exportedPath), Is.True);
                Assert.That(
                    File.ReadAllText(exportedPath),
                    Is.EqualTo(
                        File.ReadAllText(
                            IOSPrivacyManifestConfiguration
                                .AbsoluteManifestPath)));
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                    Directory.Delete(temporaryDirectory, true);
            }
        }

        private static PlistDocument LoadProductionManifest()
        {
            var manifest = new PlistDocument();
            manifest.ReadFromFile(
                IOSPrivacyManifestConfiguration.AbsoluteManifestPath);
            return manifest;
        }
    }
}
#endif
