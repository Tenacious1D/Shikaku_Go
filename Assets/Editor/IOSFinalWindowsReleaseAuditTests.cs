#if UNITY_EDITOR && UNITY_IOS
using NUnit.Framework;

namespace Shikaku.EditorTools.Tests
{
    public sealed class IOSFinalWindowsReleaseAuditTests
    {
        [Test]
        public void FinalWindowsIOSReleaseAuditPasses()
        {
            Assert.That(
                IOSFinalWindowsReleaseAudit.CollectErrors(),
                Is.Empty);
        }

        [Test]
        public void IOSAppIconIsCompleteAndOpaque()
        {
            Assert.That(
                IOSAppIconConfiguration.CollectErrors(),
                Is.Empty);
        }
    }
}
#endif
