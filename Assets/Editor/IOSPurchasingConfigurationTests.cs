using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shikaku.SaveSystem;
using Shikaku.Store;
using NUnit.Framework;
using UnityEngine.Purchasing;

namespace Shikaku.EditorTools.Tests
{
    public sealed class IOSPurchasingConfigurationTests
    {
        [Test]
        public void ProductionConfigurationPassesValidation()
        {
            IReadOnlyList<string> errors =
                IOSPurchasingReleaseConfiguration.CollectErrors();

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void CatalogContainsExactAppStoreProducts()
        {
            Assert.That(StoreProductCatalog.Products, Has.Count.EqualTo(5));
            Assert.That(
                StoreProductCatalog.Products.Select(product => product.Id),
                Is.EquivalentTo(new[]
                {
                    "remove_ads",
                    "remove_ads_hints_30",
                    "hints_10",
                    "hints_25",
                    "hints_60"
                }));
            Assert.That(
                StoreProductCatalog.Products
                    .Select(product => product.Id)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Is.EqualTo(5));
        }

        [TestCase("remove_ads", ProductType.NonConsumable, 0, true, false)]
        [TestCase(
            "remove_ads_hints_30",
            ProductType.NonConsumable,
            30,
            true,
            true)]
        [TestCase("hints_10", ProductType.Consumable, 10, false, false)]
        [TestCase("hints_25", ProductType.Consumable, 25, false, false)]
        [TestCase("hints_60", ProductType.Consumable, 60, false, false)]
        public void CatalogProductMatchesApprovedTypeAndFulfillment(
            string id,
            ProductType type,
            int hints,
            bool removesAds,
            bool grantsBonusHints)
        {
            Assert.That(
                StoreProductCatalog.TryGet(
                    id,
                    out StoreProductSpec product),
                Is.True);
            Assert.That(product.Type, Is.EqualTo(type));
            Assert.That(product.HintAmount, Is.EqualTo(hints));
            Assert.That(product.RemovesAds, Is.EqualTo(removesAds));
            Assert.That(
                product.GrantsBonusHints,
                Is.EqualTo(grantsBonusHints));
            Assert.That(
                product.Restorable,
                Is.EqualTo(type == ProductType.NonConsumable));
        }
    }

    public sealed class IOSPurchaseDeliveryTests
    {
        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(
                Path.GetTempPath(),
                "ShikakuIOSPurchaseTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            SaveManager.LoadIsolatedSaveForTesting(
                Path.Combine(_testDirectory, "save.json"));
        }

        [TearDown]
        public void TearDown()
        {
            SaveManager.Flush();
            SaveManager.ClearIsolatedSaveForTesting();

            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void RedeliveredTransactionDoesNotDuplicateHints()
        {
            Assert.That(
                HintWallet.DeliverPurchasedHints(10, "transaction-1"),
                Is.EqualTo(HintPurchaseDeliveryResult.Delivered));
            Assert.That(
                HintWallet.DeliverPurchasedHints(10, "transaction-1"),
                Is.EqualTo(
                    HintPurchaseDeliveryResult.AlreadyDelivered));
            Assert.That(HintWallet.Balance, Is.EqualTo(10));
        }

        [Test]
        public void DifferentTransactionsEachDeliverOnce()
        {
            Assert.That(
                HintWallet.DeliverPurchasedHints(10, "transaction-1"),
                Is.EqualTo(HintPurchaseDeliveryResult.Delivered));
            Assert.That(
                HintWallet.DeliverPurchasedHints(25, "transaction-2"),
                Is.EqualTo(HintPurchaseDeliveryResult.Delivered));
            Assert.That(HintWallet.Balance, Is.EqualTo(35));
        }
    }
}
