// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault.Tests
{
    /// <summary>
    /// Bypasses InitializeStore (which requires a fully-populated dynamic config)
    /// since we pre-set AzClient directly in tests.
    /// </summary>
    public class TestableDiscovery : Discovery
    {
        public TestableDiscovery(IPAMSecretResolver resolver) : base(resolver) { }
        public override void InitializeStore(dynamic config) { /* no-op — AzClient already set */ }
    }

    public class TestableInventory : Inventory
    {
        public TestableInventory(IPAMSecretResolver resolver) : base(resolver) { }
        public override void InitializeStore(dynamic config) { /* no-op — AzClient already set */ }
    }

    public class DiscoveryTests
    {
        private const long JobHistoryId = 99;

        // ── Success ───────────────────────────────────────────────────────────

        [Fact]
        public void Discovery_VaultsFound_NoWarnings_ReturnsSuccess()
        {
            var vaults = new List<string> { "sub1:rg1:vault1", "sub1:rg1:vault2" };
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Returns((vaults, new List<string>()));

            List<string> submitted = null;
            var result = job.ProcessJob(BuildConfig(), v => { submitted = v?.ToList(); return true; });

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            submitted.Should().BeEquivalentTo(vaults);
        }

        [Fact]
        public void Discovery_NoVaults_NoWarnings_ReturnsSuccess()
        {
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Returns((new List<string>(), new List<string>()));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        // ── Warning ───────────────────────────────────────────────────────────

        [Fact]
        public void Discovery_SomeVaults_SomeWarnings_ReturnsWarning()
        {
            var vaults = new List<string> { "sub1:rg1:vault1" };
            var warnings = new List<string> { "Could not access tenant xyz" };
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Returns((vaults, warnings));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Warning);
            result.FailureMessage.Should().Contain("xyz");
        }

        // ── Failure ───────────────────────────────────────────────────────────

        [Fact]
        public void Discovery_NoVaults_WithWarnings_ReturnsFail()
        {
            var warnings = new List<string> { "auth failed for tenant abc" };
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Returns((new List<string>(), warnings));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("abc");
        }

        [Fact]
        public void Discovery_GetVaultsThrows_ReturnsFail()
        {
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Throws(new Exception("network timeout"));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("network timeout");
        }

        // ── Truncation ────────────────────────────────────────────────────────

        [Fact]
        public void Discovery_LongFailureMessage_IsTruncated()
        {
            var longWarning = new string('x', 4500);
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults())
                .Returns((new List<string>(), new List<string> { longWarning }));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Length.Should().BeLessThanOrEqualTo(4000);
            result.FailureMessage.Should().Contain("truncated");
        }

        [Fact]
        public void Discovery_SuccessResult_IsNeverTruncated()
        {
            // Regression: the old code ran the truncation check unconditionally,
            // which could mangle a success result. Verify it no longer does.
            var vaults = new List<string> { "sub1:rg1:vault1" };
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetVaults()).Returns((vaults, new List<string>()));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            result.FailureMessage.Should().NotContain("truncated");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Discovery BuildJob(out Mock<AzureClient> mockClient)
        {
            mockClient = new Mock<AzureClient>();
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            return new TestableDiscovery(resolverMock.Object)
            {
                AzClient = mockClient.Object,
                VaultProperties = new AkvProperties
                {
                    TenantId = "test-tenant-id",
                    TenantIdsForDiscovery = new List<string> { "test-tenant-id" }
                },
                Logger = LogHandler.GetClassLogger<Discovery>()
            };
        }

        private static DiscoveryJobConfiguration BuildConfig() =>
            new DiscoveryJobConfiguration
            {
                JobHistoryId = JobHistoryId,
                ClientMachine = "test-tenant-id",
                JobProperties = new Dictionary<string, object> { { "dirs", "test-tenant-id" } }
            };
    }

    public class InventoryTests
    {
        private const long JobHistoryId = 77;

        // ── Success ───────────────────────────────────────────────────────────

        [Fact]
        public void Inventory_CertsReturned_CallsCallbackAndSucceeds()
        {
            var inventoryItems = new List<CurrentInventoryItem>
            {
                new CurrentInventoryItem { Alias = "cert1", PrivateKeyEntry = true },
                new CurrentInventoryItem { Alias = "cert2", PrivateKeyEntry = true }
            };

            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetCertificatesAsync()).ReturnsAsync(inventoryItems);

            List<CurrentInventoryItem> submitted = null;
            var result = job.ProcessJob(BuildConfig(), items => { submitted = items?.ToList(); return true; });

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            submitted.Should().HaveCount(2);
            submitted.Should().Contain(i => i.Alias == "cert1");
            submitted.Should().Contain(i => i.Alias == "cert2");
        }

        [Fact]
        public void Inventory_EmptyVault_CallsCallbackWithEmptyList()
        {
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetCertificatesAsync())
                .ReturnsAsync(new List<CurrentInventoryItem>());

            List<CurrentInventoryItem> submitted = null;
            var result = job.ProcessJob(BuildConfig(), items => { submitted = items?.ToList(); return true; });

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            submitted.Should().BeEmpty();
        }

        // ── Failure ───────────────────────────────────────────────────────────

        [Fact]
        public void Inventory_GetCertificatesThrows_ReturnsFail()
        {
            var job = BuildJob(out var mockClient);
            mockClient.Setup(c => c.GetCertificatesAsync())
                .ThrowsAsync(new Exception("vault access denied"));

            var result = job.ProcessJob(BuildConfig(), _ => true);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("vault access denied");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Inventory BuildJob(out Mock<AzureClient> mockClient)
        {
            mockClient = new Mock<AzureClient>();
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            return new TestableInventory(resolverMock.Object)
            {
                AzClient = mockClient.Object,
                VaultProperties = new AkvProperties { VaultName = "test-vault" },
                Logger = LogHandler.GetClassLogger<Inventory>()
            };
        }

        private static InventoryJobConfiguration BuildConfig() =>
            new InventoryJobConfiguration { JobHistoryId = JobHistoryId };
    }
}
