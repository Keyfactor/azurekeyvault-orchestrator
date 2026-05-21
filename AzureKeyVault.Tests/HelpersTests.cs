// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using FluentAssertions;
using Org.BouncyCastle.Pkcs;
using Xunit;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault.Tests
{
    public class HelpersTests
    {
        // ── ConvertPfxToPasswordlessPkcs12 ────────────────────────────────────

        [Fact]
        public void ConvertPfx_Rsa2048_ReturnsCorrectKeyTypeAndSize()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa2048Base64, CertificateFixtures.PfxPassword);

            result.KeyType.Should().Be("RSA");
            result.KeySize.Should().Be(2048);
        }

        [Fact]
        public void ConvertPfx_Rsa4096_ReturnsCorrectKeyTypeAndSize()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa4096Base64, CertificateFixtures.PfxPassword);

            result.KeyType.Should().Be("RSA");
            result.KeySize.Should().Be(4096);
        }

        [Fact]
        public void ConvertPfx_EcP256_ReturnsCorrectKeyTypeAndNullSize()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.EcP256Base64, CertificateFixtures.PfxPassword);

            result.KeyType.Should().Be("EC");
            result.KeySize.Should().BeNull();
        }

        [Fact]
        public void ConvertPfx_Rsa2048_OutputIsValidPasswordlessPkcs12()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa2048Base64, CertificateFixtures.PfxPassword);

            result.CertBytes.Should().NotBeNullOrEmpty();

            var store = new Pkcs12StoreBuilder().Build();
            var action = () => store.Load(
                new System.IO.MemoryStream(result.CertBytes), null);

            action.Should().NotThrow("output should be a valid passwordless PKCS#12");
        }

        [Fact]
        public void ConvertPfx_Rsa4096_OutputIsValidPasswordlessPkcs12()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa4096Base64, CertificateFixtures.PfxPassword);

            result.CertBytes.Should().NotBeNullOrEmpty();

            var store = new Pkcs12StoreBuilder().Build();
            var action = () => store.Load(
                new System.IO.MemoryStream(result.CertBytes), null);

            action.Should().NotThrow();
        }

        [Fact]
        public void ConvertPfx_OutputContainsPrivateKey()
        {
            var result = Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa2048Base64, CertificateFixtures.PfxPassword);

            var store = new Pkcs12StoreBuilder().Build();
            store.Load(new System.IO.MemoryStream(result.CertBytes), null);

            bool hasKeyEntry = false;
            foreach (string alias in store.Aliases)
            {
                if (store.IsKeyEntry(alias))
                {
                    hasKeyEntry = true;
                    break;
                }
            }

            hasKeyEntry.Should().BeTrue("the private key should be preserved in the output");
        }

        [Fact]
        public void ConvertPfx_WrongPassword_Throws()
        {
            var action = () => Helpers.ConvertPfxToPasswordlessPkcs12(
                CertificateFixtures.Rsa2048Base64, "wrong-password");

            action.Should().Throw<Exception>("an incorrect password should fail to decrypt the PFX");
        }

        [Fact]
        public void ConvertPfx_InvalidBase64_Throws()
        {
            var action = () => Helpers.ConvertPfxToPasswordlessPkcs12(
                "not-valid-base64!!!", CertificateFixtures.PfxPassword);

            action.Should().Throw<Exception>();
        }

        // ── IsValidJson ───────────────────────────────────────────────────────

        [Fact]
        public void IsValidJson_ValidObject_ReturnsTrue()
        {
            "{\"key\": \"value\"}".IsValidJson().Should().BeTrue();
        }

        [Fact]
        public void IsValidJson_ValidArray_ReturnsTrue()
        {
            "[\"a\", \"b\", \"c\"]".IsValidJson().Should().BeTrue();
        }

        [Fact]
        public void IsValidJson_EmptyObject_ReturnsTrue()
        {
            "{}".IsValidJson().Should().BeTrue();
        }

        [Fact]
        public void IsValidJson_InvalidJson_ReturnsFalse()
        {
            "this is not json".IsValidJson().Should().BeFalse();
        }

        [Fact]
        public void IsValidJson_MalformedJson_ReturnsFalse()
        {
            "{\"key\": }".IsValidJson().Should().BeFalse();
        }

        [Fact]
        public void IsValidJson_EmptyString_ReturnsFalse()
        {
            "".IsValidJson().Should().BeFalse();
        }

        [Fact]
        public void IsValidJson_TagsStyleJson_ReturnsTrue()
        {
            // Mirrors the actual tags format used in Management jobs
            "{\"env\": \"prod\", \"owner\": \"team-platform\"}".IsValidJson().Should().BeTrue();
        }
    }
}
