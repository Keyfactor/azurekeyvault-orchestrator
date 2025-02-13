
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public static class Helpers
    {
        public static bool IsValidJson(this string jsonString)
        {
            try
            {
                var unescapedJSON = System.Text.RegularExpressions.Regex.Unescape(jsonString);
                var tmpObj = JsonValue.Parse(unescapedJSON);
            }
            catch (FormatException fex)
            {
                //Invalid json format
                return false;
            }
            catch (Exception ex) //some other exception
            {
                return false;
            }
            return true;
        }
        public static byte[] ConvertPfxToPasswordlessPkcs12(string base64Pfx, string pfxPassword)
        {
            // Decode the Base64-encoded PFX data
            byte[] pfxBytes = Convert.FromBase64String(base64Pfx);
            using (var inputStream = new MemoryStream(pfxBytes))
            {
                var builder = new Pkcs12StoreBuilder();
                builder.SetUseDerEncoding(true);
                var store = builder.Build();
                store.Load(inputStream, pfxPassword.ToCharArray());

                string alias = null;
                foreach (string a in store.Aliases)
                {
                    if (store.IsKeyEntry(a))
                    {
                        alias = a;
                        break;
                    }
                }

                using (var outputStream = new MemoryStream())
                {
                    var newStore = builder.Build();

                    if (alias != null)
                    {
                        // Extract private key and certificate chain if available
                        var keyEntry = store.GetKey(alias);
                        var chain = store.GetCertificateChain(alias);
                        newStore.SetKeyEntry("converted-key", keyEntry, chain);
                    }
                    else
                    {
                        // If no private key, include just the certificate chain
                        foreach (string certAlias in store.Aliases)
                        {
                            if (store.IsCertificateEntry(certAlias))
                            {
                                var cert = store.GetCertificate(certAlias);
                                newStore.SetCertificateEntry(certAlias, cert);
                            }
                        }
                    }

                    // Save the new PKCS#12 store without a password
                    newStore.Save(outputStream, null, new SecureRandom());
                    return outputStream.ToArray();
                }
            }
        }
    }
}
