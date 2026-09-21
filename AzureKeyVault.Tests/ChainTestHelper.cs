// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault.Tests
{
    /// <summary>
    /// Builds a synthetic root/intermediate/leaf certificate chain, with proper
    /// AuthorityKeyIdentifier/SubjectKeyIdentifier extensions (mirroring a normal
    /// CA-issued chain), and lets tests hand-assemble a PKCS#12 file with the cert
    /// bags in a caller-chosen physical order - so tests can prove whether
    /// downstream code preserves or reorders that physical order.
    /// </summary>
    public static class ChainTestHelper
    {
        static ChainTestHelper()
        {
            // Lets Pkcs12Store.Load accept a password on the unencrypted, no-MAC PFX
            // files this helper builds by hand for tests. Harmless for every other
            // test in this project: it only changes behavior for the specific
            // "password supplied but keystore has no MacData" case, which no other
            // test in this project exercises.
            Environment.SetEnvironmentVariable("Org.BouncyCastle.Pkcs12.IgnoreUselessPassword", "true");
        }

        public class TestChain
        {
            public X509Certificate Root;
            public X509Certificate Intermediate;
            public X509Certificate Leaf;
            public AsymmetricCipherKeyPair LeafKeys;
        }

        public static TestChain BuildChain()
        {
            var rootKpGen = new RsaKeyPairGenerator();
            rootKpGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var rootKeys = rootKpGen.GenerateKeyPair();

            var rootGen = new X509V3CertificateGenerator();
            rootGen.SetSerialNumber(BigInteger.ValueOf(1));
            rootGen.SetIssuerDN(new X509Name("CN=Test Root CA"));
            rootGen.SetSubjectDN(new X509Name("CN=Test Root CA"));
            rootGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            rootGen.SetNotAfter(DateTime.UtcNow.AddYears(10));
            rootGen.SetPublicKey(rootKeys.Public);
            rootGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(true));
            rootGen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(rootKeys.Public));
            var rootCert = rootGen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", rootKeys.Private, new SecureRandom()));

            var (intermediateKeys, intermediateCert) = MakeCert("CN=Test Intermediate CA", "CN=Test Root CA", rootKeys);
            var (leafKeys, leafCert) = MakeCert("CN=test-leaf.example.com", "CN=Test Intermediate CA", intermediateKeys);

            return new TestChain
            {
                Root = rootCert,
                Intermediate = intermediateCert,
                Leaf = leafCert,
                LeafKeys = leafKeys
            };
        }

        private static (AsymmetricCipherKeyPair keys, X509Certificate cert) MakeCert(
            string subject, string issuer, AsymmetricCipherKeyPair issuerKeys)
        {
            var kpGen = new RsaKeyPairGenerator();
            kpGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var subjectKeys = kpGen.GenerateKeyPair();

            var gen = new X509V3CertificateGenerator();
            gen.SetSerialNumber(BigInteger.ValueOf(Math.Abs(Guid.NewGuid().GetHashCode()) + 1));
            gen.SetIssuerDN(new X509Name(issuer));
            gen.SetSubjectDN(new X509Name(subject));
            gen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            gen.SetNotAfter(DateTime.UtcNow.AddYears(5));
            gen.SetPublicKey(subjectKeys.Public);
            gen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            gen.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(subjectKeys.Public));
            gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifierStructure(issuerKeys.Public));

            var cert = gen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", issuerKeys.Private, new SecureRandom()));
            return (subjectKeys, cert);
        }

        /// <summary>
        /// Hand-assembles an unencrypted PKCS#12 file with the given certs as CertBags
        /// in exactly the order provided, plus a KeyBag for the chain's leaf private key.
        /// Bypasses Pkcs12Store entirely for construction, so the physical bag order is
        /// fully known and controllable (unlike going through SetKeyEntry/Save).
        /// </summary>
        public static byte[] BuildPkcs12WithCertOrder(TestChain chain, params X509Certificate[] certsInOrder)
        {
            byte[] localKeyId = { 0x01 };
            Asn1Set LocalKeyIdAttr() => new DerSet(new DerSequence(PkcsObjectIdentifiers.Pkcs9AtLocalKeyID, new DerSet(new DerOctetString(localKeyId))));

            var keyBag = new SafeBag(PkcsObjectIdentifiers.KeyBag,
                PrivateKeyInfoFactory.CreatePrivateKeyInfo(chain.LeafKeys.Private).ToAsn1Object(),
                LocalKeyIdAttr());

            var certBags = new List<SafeBag>();
            foreach (var cert in certsInOrder)
            {
                bool isLeaf = cert.Equals(chain.Leaf);
                certBags.Add(new SafeBag(PkcsObjectIdentifiers.CertBag,
                    new CertBag(PkcsObjectIdentifiers.X509Certificate, new DerOctetString(cert.GetEncoded())).ToAsn1Object(),
                    isLeaf ? LocalKeyIdAttr() : null));
            }

            var keyContent = new ContentInfo(PkcsObjectIdentifiers.Data,
                new BerOctetString(new DerSequence(keyBag).GetDerEncoded()));
            var certContent = new ContentInfo(PkcsObjectIdentifiers.Data,
                new BerOctetString(new DerSequence(certBags.ToArray()).GetDerEncoded()));

            byte[] authSafeEncoded = new AuthenticatedSafe(new[] { keyContent, certContent }).GetEncoded("DER");
            var outerContent = new ContentInfo(PkcsObjectIdentifiers.Data, new BerOctetString(authSafeEncoded));
            var pfx = new Pfx(outerContent, null);
            return pfx.GetEncoded("DER");
        }

        /// <summary>
        /// Walks the physical (on-disk) bag order of an unencrypted PKCS#12 file's
        /// cert bags and returns their Subject DNs in that exact order - without
        /// re-deriving/canonicalizing via Pkcs12Store.GetCertificateChain, which
        /// would mask any reordering bug we're trying to detect.
        /// </summary>
        public static List<string> GetPhysicalCertBagSubjectOrder(byte[] pkcs12Bytes)
        {
            var subjects = new List<string>();
            var pfx = Pfx.GetInstance(Asn1Object.FromByteArray(pkcs12Bytes));
            byte[] authSafeOctets = Asn1OctetString.GetInstance(pfx.AuthSafe.Content).GetOctets();
            var authSafe = AuthenticatedSafe.GetInstance(Asn1Object.FromByteArray(authSafeOctets));

            var parser = new X509CertificateParser();
            foreach (var ci in authSafe.GetContentInfo())
            {
                if (!ci.ContentType.Equals(PkcsObjectIdentifiers.Data))
                {
                    continue; // encrypted safe-contents; not used by these test fixtures
                }

                byte[] safeContentsOctets = Asn1OctetString.GetInstance(ci.Content).GetOctets();
                var bags = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(safeContentsOctets));

                foreach (Asn1Encodable bagObj in bags)
                {
                    var safeBag = SafeBag.GetInstance(bagObj);
                    if (!safeBag.BagID.Equals(PkcsObjectIdentifiers.CertBag))
                    {
                        continue;
                    }

                    var certBag = CertBag.GetInstance(safeBag.BagValue);
                    byte[] certDer = Asn1OctetString.GetInstance(certBag.CertValue).GetOctets();
                    var cert = parser.ReadCertificate(certDer);
                    subjects.Add(cert.SubjectDN.ToString());
                }
            }

            return subjects;
        }
    }
}
