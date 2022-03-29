// Copyright 2022 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Azure.Management.KeyVault;
using Microsoft.Azure.Management.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AzureClient
    {
        private protected virtual CertificateClient KeyVaultClient { get; set; }
        internal protected virtual string applicationId { get; set; }
        internal protected virtual string clientSecret { get; set; }
        internal protected virtual string tenantId { get; set; }
        internal protected virtual HttpClient HttpClient { get; set; }
        internal protected virtual ArmClient armClient { get; set; }

        internal protected virtual KeyVaultManagementClient kvManagementClient { get; set; }

        ILogger logger { get; set; }

        public AzureClient(){}
        public AzureClient(string appId, string tenantId, string clientSecret, string vaultUrl = null)
        {
            applicationId = appId;
            this.tenantId = tenantId;
            this.clientSecret = clientSecret;

            if (vaultUrl != null)
                KeyVaultClient = new CertificateClient(vaultUri: new Uri(vaultUrl), credential: new ClientSecretCredential(tenantId, appId, clientSecret));

            logger = LogHandler.GetClassLogger<AzureClient>();
        }

        public virtual async Task<DeleteCertificateOperation> DeleteCertificateAsync(string certName)
        {
            return await KeyVaultClient.StartDeleteCertificateAsync(certName);
        }

        public virtual async Task<Vault> CreateVault(string subscriptionId, string resourceGroupName, string vaultName, string apiObjectId)
        {
            var creds = SdkContext.AzureCredentialsFactory.FromServicePrincipal(applicationId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);

            kvManagementClient = new KeyVaultManagementClient(creds) { SubscriptionId = subscriptionId };

            var vaults = kvManagementClient.Vaults;

            var vaultProperties = new VaultProperties(new Guid(tenantId), new Sku(SkuName.Standard))
            {
                AccessPolicies = new[] {new AccessPolicyEntry()
                    {
                        ApplicationId = new Guid(applicationId),
                        ObjectId = apiObjectId,
                        Permissions = new Permissions()
                        {
                            Certificates = new List<string>
                            {
                                "get", "list", "delete", "create", "import", "update", "managecontacts", "getissuers",
                                "listissuers", "setissuers", "deleteissuers", "manageissuers", "recover","purge"
                            },
                            Keys = new List<string>(),
                            Secrets = new List<string>(),
                            Storage = new List<string>()
                        },
                        TenantId = new Guid(tenantId)
                    } }
            };
            var vaultParameters = new VaultCreateOrUpdateParameters("eastus", vaultProperties);
            Vault newVault;

            try
            {
                logger.LogInformation("Begin create vault...");
                newVault = await vaults.BeginCreateOrUpdateAsync(resourceGroupName, vaultName, vaultParameters); //this takes a bit of time to run
            }
            catch (Exception ex)
            {
                logger.LogError("Error when trying to create Azure Keyvault", ex);
                throw;
            }
            logger.LogInformation(@"Created vault {0}", newVault.Name);

            return newVault;
        }

        public virtual async Task<CertificateOperation> CreateCertificateAsync(string vaultUrl, string certName)
        {
            return await KeyVaultClient.StartCreateCertificateAsync(certName, policy: new CertificatePolicy());
        }

        public virtual async Task<KeyVaultCertificateWithPolicy> ImportCertificateAsync(string vaultUrl, string name, X509Certificate2 certificate)
        {
            var importOptions = new ImportCertificateOptions(name, certificate.RawData);
            return await KeyVaultClient.ImportCertificateAsync(importOptions);
        }

        public virtual async Task<IEnumerable<CurrentInventoryItem>> GetCertificatesAsync(string vaultUrl)
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            Pageable<CertificateProperties> allCertificates = KeyVaultClient.GetPropertiesOfCertificates();
            var certs = new List<CertificateProperties>();

            foreach (var certificate in allCertificates)
            {
                var cert = await KeyVaultClient.GetCertificateAsync(certificate.Name);

                inventoryItems.Add(new CurrentInventoryItem()
                {
                    Alias = certificate.Name,
                    PrivateKeyEntry = true,
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    UseChainLevel = true,
                    Certificates = new string[] { Convert.ToBase64String(cert.Value.Cer) }
                });
            }
            return inventoryItems;
        }

        public virtual async Task<_DiscoveryResult> GetVaults(string subscriptionId)
        {
            string result = string.Empty;
            HttpClient ??= new HttpClient();

            var uri = $"https://management.azure.com/subscriptions/{subscriptionId}/resources?%24filter=resourceType%20eq%20%27Microsoft.KeyVault%2Fvaults%27&api-version=2018-05-01";
            var token = await AcquireTokenBySPN(tenantId, applicationId, clientSecret);
            HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            HttpResponseMessage resp = await HttpClient.GetAsync(uri);
            result = resp.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<_DiscoveryResult>(result);
        }

        private async Task<string> AcquireTokenBySPN(string tenantId, string clientId, string clientSecret)
        {
            HttpClient ??= new HttpClient();

            const string ARMResource = "https://management.azure.com/";
            const string TokenEndpoint = "https://login.windows.net/{0}/oauth2/token";
            const string SPNPayload = "resource={0}&client_id={1}&grant_type=client_credentials&client_secret={2}";

            var payload = string.Format(SPNPayload,
                                        WebUtility.UrlEncode(ARMResource),
                                        WebUtility.UrlEncode(clientId),
                                        WebUtility.UrlEncode(clientSecret));

            var address = string.Format(TokenEndpoint, tenantId);
            var content = new StringContent(payload, Encoding.UTF8, "application/x-www-form-urlencoded");
            dynamic body;
            using (var response = await HttpClient.PostAsync(address, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Error getting azure token: status code {0};  response content: {1}", response.StatusCode, await response.Content.ReadAsStringAsync());
                }

                response.EnsureSuccessStatusCode();
                var stringContent = await response.Content.ReadAsStringAsync();
                body = JsonConvert.DeserializeObject(stringContent, typeof(ExpandoObject));
            }

            return body.access_token;
        }
    }

    public class CreateVaultRequest
    {
        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("properties")]
        public VaultProperties Properties { get; set; }
    }
}

