// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using Azure.Security.KeyVault.Certificates;
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
    /// Thin subclass that promotes the protected PerformAddition/PerformRemoval
    /// methods to public so the test project can call them directly.
    /// </summary>
    public class TestableManagement : Management
    {
        public TestableManagement(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult CallPerformAddition(
            string alias, string pfxPassword, string entryContents,
            string tagsJSON, long jobHistoryId, bool overwrite,
            bool preserveTags, bool nonExportable)
            => PerformAddition(alias, pfxPassword, entryContents,
                tagsJSON, jobHistoryId, overwrite, preserveTags, nonExportable);

        public JobResult CallPerformRemoval(string alias, string tagsJSON, long jobHistoryId)
            => PerformRemoval(alias, tagsJSON, jobHistoryId);
    }

    public class ManagementTests
    {
        private const string Alias = "my-cert";
        private const string EmptyTags = "";
        private const long JobHistoryId = 42;

        // ── Add: success cases ────────────────────────────────────────────────

        [Fact]
        public void Add_Rsa2048_NewCert_Succeeds()
        {
            var job = BuildJob(out var mockClient);
            SetupImportSuccess(mockClient, Alias);

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        [Fact]
        public void Add_Rsa4096_NewCert_Succeeds()
        {
            var job = BuildJob(out var mockClient);
            SetupImportSuccess(mockClient, Alias);

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa4096Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        [Fact]
        public void Add_EcCert_NewCert_Succeeds()
        {
            var job = BuildJob(out var mockClient);
            SetupImportSuccess(mockClient, Alias);

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.EcP256Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        [Fact]
        public void Add_WithTags_PassesTagsToImport()
        {
            var job = BuildJob(out var mockClient);
            SetupImportSuccess(mockClient, Alias);

            var tagsJson = "{\"env\": \"prod\", \"owner\": \"platform\"}";
            job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                tagsJson, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            mockClient.Verify(c => c.ImportCertificateAsync(
                Alias,
                It.IsAny<string>(),
                CertificateFixtures.PfxPassword,
                It.Is<Dictionary<string, string>>(d =>
                    d.ContainsKey("env") && d["env"] == "prod" &&
                    d.ContainsKey("owner") && d["owner"] == "platform"),
                false), Times.Once);
        }

        // ── Add: failure / warning cases ──────────────────────────────────────

        [Fact]
        public void Add_EmptyAlias_ReturnsFail()
        {
            var job = BuildJob(out _);

            var result = job.CallPerformAddition(
                alias: "", CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("alias");
        }

        [Fact]
        public void Add_NullAlias_ReturnsFail()
        {
            var job = BuildJob(out _);

            var result = job.CallPerformAddition(
                alias: null, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("alias");
        }

        [Fact]
        public void Add_OverwriteFalse_CertExists_ReturnsWarning()
        {
            var job = BuildJob(out var mockClient);
            mockClient
                .Setup(c => c.GetCertificate(Alias))
                .ReturnsAsync(MakeFakeCertificate(Alias));

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: false, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Warning);
            result.FailureMessage.Should().Contain(Alias);
            result.FailureMessage.Should().Contain("overwrite");
            mockClient.Verify(c => c.ImportCertificateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void Add_OverwriteTrue_CertExists_Succeeds()
        {
            var job = BuildJob(out var mockClient);
            mockClient
                .Setup(c => c.GetCertificate(Alias))
                .ReturnsAsync(MakeFakeCertificate(Alias));
            SetupImportSuccess(mockClient, Alias);

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        [Fact]
        public void Add_ImportThrows_ReturnsFailWithMessage()
        {
            var job = BuildJob(out var mockClient);
            mockClient
                .Setup(c => c.ImportCertificateAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception("AKV import failed"));

            var result = job.CallPerformAddition(
                Alias, CertificateFixtures.PfxPassword, CertificateFixtures.Rsa2048Base64,
                EmptyTags, JobHistoryId, overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("AKV import failed");
        }

        [Fact]
        public void Add_NoPfxPassword_ReturnsFail()
        {
            var job = BuildJob(out _);

            var result = job.CallPerformAddition(
                Alias, pfxPassword: "", entryContents: CertificateFixtures.Rsa2048Base64,
                tagsJSON: EmptyTags, jobHistoryId: JobHistoryId,
                overwrite: true, preserveTags: false, nonExportable: false);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("PFX");
        }

        // ── Remove ────────────────────────────────────────────────────────────

        [Fact]
        public void Remove_ValidAlias_Succeeds()
        {
            var job = BuildJob(out var mockClient);
            SetupDeleteSuccess(mockClient, Alias);

            var result = job.CallPerformRemoval(Alias, EmptyTags, JobHistoryId);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
        }

        [Fact]
        public void Remove_EmptyAlias_ReturnsFail()
        {
            var job = BuildJob(out _);

            var result = job.CallPerformRemoval(alias: "", EmptyTags, JobHistoryId);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("alias");
        }

        [Fact]
        public void Remove_DeleteThrows_ReturnsFailWithMessage()
        {
            var job = BuildJob(out var mockClient);
            mockClient
                .Setup(c => c.DeleteCertificateAsync(Alias))
                .ThrowsAsync(new Exception("vault unreachable"));

            var result = job.CallPerformRemoval(Alias, EmptyTags, JobHistoryId);

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("vault unreachable");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static TestableManagement BuildJob(out Mock<AzureClient> mockClient)
        {
            mockClient = new Mock<AzureClient>();
            mockClient
                .Setup(c => c.GetCertificate(It.IsAny<string>()))
                .ReturnsAsync((KeyVaultCertificateWithPolicy)null);

            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            return new TestableManagement(resolverMock.Object)
            {
                AzClient = mockClient.Object,
                VaultProperties = new AkvProperties { VaultName = "test-vault" },
                Logger = LogHandler.GetClassLogger<Management>()
            };
        }

        /// <summary>
        /// Creates a minimal KeyVaultCertificateWithPolicy for "cert exists" scenarios
        /// (overwrite/tags tests). Uses CertificateModelFactory — the SDK's test helper.
        /// </summary>
        private static KeyVaultCertificateWithPolicy MakeFakeCertificate(string name)
        {
            var props = CertificateModelFactory.CertificateProperties(name: name);
            return CertificateModelFactory.KeyVaultCertificateWithPolicy(properties: props);
        }

        private static void SetupImportSuccess(Mock<AzureClient> mockClient, string alias)
        {
            // Build a cert with Version set so PerformAddition's success check passes.
            // X509Thumbprint isn't a factory parameter in this SDK version so we set it
            // via reflection after construction.
            var props = CertificateModelFactory.CertificateProperties(
                name: alias,
                version: "abc123def456");
            var cert = CertificateModelFactory.KeyVaultCertificateWithPolicy(properties: props);

            // Set X509Thumbprint — try all known field name patterns across SDK versions
            var thumbField = FindField(typeof(CertificateProperties), "_x509Thumbprint")
                          ?? FindField(typeof(CertificateProperties), "_X509Thumbprint")
                          ?? FindField(typeof(CertificateProperties), "<X509Thumbprint>k__BackingField");
            thumbField?.SetValue(cert.Properties, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

            mockClient
                .Setup(c => c.ImportCertificateAsync(
                    alias, It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<bool>()))
                .ReturnsAsync(cert);
        }

        private static void SetupDeleteSuccess(Mock<AzureClient> mockClient, string alias)
        {
            var props = CertificateModelFactory.CertificateProperties(name: alias);
            var deletedCert = CertificateModelFactory.DeletedCertificate(properties: props);

            var opMock = new Mock<DeleteCertificateOperation>();
            opMock.Setup(o => o.Value).Returns(deletedCert);
            mockClient.Setup(c => c.DeleteCertificateAsync(alias)).ReturnsAsync(opMock.Object);
        }

        /// <summary>Searches the type hierarchy for a private/protected field by name.</summary>
        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                var f = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f;
                type = type.BaseType;
            }
            return null;
        }
    }
}
