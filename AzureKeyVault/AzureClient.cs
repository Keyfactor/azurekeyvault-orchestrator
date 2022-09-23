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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Administration;
using Azure.Security.KeyVault.Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Azure.Management.KeyVault;
using Microsoft.Azure.Management.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AzureClient
    {
        internal protected virtual AkvProperties VaultProperties { get; set; }
        ILogger logger { get; set; }

        private protected virtual CertificateClient CertClient
        {
            get
            {
                if (_certClient != null)
                {
                    return _certClient;
                }
                _certClient = new CertificateClient(new Uri(VaultProperties.VaultURL), credential: new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ApplicationId, VaultProperties.ClientSecret));
                return _certClient;
            }
        }
        protected CertificateClient _certClient { get; set; }

        internal protected virtual KeyVaultManagementClient KvManagementClient
        {
            get
            {
                if (_mgmtClient != null)
                {
                    return _mgmtClient;
                }
                var subId = VaultProperties.SubscriptionId ?? VaultProperties.StorePath.Split("/")[2];
                var creds = SdkContext.AzureCredentialsFactory.FromServicePrincipal(VaultProperties.ApplicationId, VaultProperties.ClientSecret, VaultProperties.TenantId, AzureEnvironment.AzureGlobalCloud);
                _mgmtClient = new KeyVaultManagementClient(creds) { SubscriptionId = subId };
                return _mgmtClient;
            }
        }
        protected virtual KeyVaultManagementClient _mgmtClient { get; set; }

        public AzureClient() { }
        public AzureClient(AkvProperties props)
        {
            VaultProperties = props;
            logger = LogHandler.GetClassLogger<AzureClient>();
        }

        public virtual async Task<DeleteCertificateOperation> DeleteCertificateAsync(string certName)
        {
            return await CertClient.StartDeleteCertificateAsync(certName);
        }

        public virtual async Task<Vault> CreateVault()
        {
            var vaults = KvManagementClient.Vaults;

            var newVaultProps = new VaultProperties(new Guid(VaultProperties.TenantId), new Sku(SkuName.Standard))
            {
                AccessPolicies = new[] {new AccessPolicyEntry()
                    {
                        ApplicationId = new Guid(VaultProperties.ApplicationId),
                        ObjectId = VaultProperties.ObjectId,
                        Permissions = new Permissions()
                        {
                            Certificates = new List<string>
                            {
                               CertificatePermissions.All
                            },
                        },
                        TenantId = new Guid(VaultProperties.TenantId)
                    } }
            };
            var vaultParameters = new VaultCreateOrUpdateParameters("eastus", newVaultProps);
            Vault newVault;

            try
            {
                logger.LogInformation("Begin create vault...");

                newVault = await vaults.BeginCreateOrUpdateAsync(VaultProperties.ResourceGroupName, VaultProperties.VaultName, vaultParameters); //this takes a bit of time to run
            }
            catch (Exception ex)
            {
                logger.LogError("Error when trying to create Azure Keyvault", ex);
                throw;
            }
            logger.LogInformation(@"Created vault {0}", newVault.Name);

            return newVault;
        }

        public virtual async Task<KeyVaultCertificateWithPolicy> ImportCertificateAsync(string certName, string contents, string pfxPassword)
        {
            try
            {
                var bytes = Convert.FromBase64String(contents);
                var x509 = new X509Certificate2(bytes, pfxPassword, X509KeyStorageFlags.Exportable);                
                var certWithKey = x509.Export(X509ContentType.Pkcs12);
                var cert = await CertClient.ImportCertificateAsync(new ImportCertificateOptions(certName, certWithKey));
                return cert;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                throw;
            }
        }

        public virtual async Task<IEnumerable<CurrentInventoryItem>> GetCertificatesAsync()
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            Pageable<CertificateProperties> inventory = null;
            try
            {
                inventory = CertClient.GetPropertiesOfCertificates();
                var certQuantity = inventory.Count();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("does not have certificates list permission"))
                {
                    await SetManagementPermissionsForService();
                    inventory = CertClient.GetPropertiesOfCertificates();
                }
                else throw;
            }

            foreach (var certificate in inventory)
            {
                var cert = await CertClient.GetCertificateAsync(certificate.Name);
                inventoryItems.Add(new CurrentInventoryItem()
                {
                    Alias = cert.Value.Name,
                    PrivateKeyEntry = true,
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    UseChainLevel = true,
                    Certificates = new string[] { Convert.ToBase64String(cert.Value.Cer) }
                });
            }
            return inventoryItems;
        }

        public virtual async Task<List<string>> GetVaults()
        {
            var resources = await KvManagementClient.Vaults.ListAsync();
            return resources.Select(v => v.Id).ToList();
        }


        private async Task SetManagementPermissionsForService()
        {
            KeyVaultAccessControlClient client = new KeyVaultAccessControlClient(new Uri(VaultProperties.VaultURL), new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ApplicationId, VaultProperties.ClientSecret));

            var vaults = KvManagementClient.Vaults;

            var permissions = new Permissions
            {
                Certificates = new List<string>() {
                    CertificatePermissions.All
                }
            };
            var accessPolicyEntry = new AccessPolicyEntry(new Guid(VaultProperties.TenantId), VaultProperties.ObjectId, permissions);
            var accessPolicyProperties = new VaultAccessPolicyProperties(new[] { accessPolicyEntry });
            var accessPolicyParameters = new VaultAccessPolicyParameters(accessPolicyProperties);
            try
            {
                await vaults.UpdateAccessPolicyAsync(VaultProperties.ResourceGroupName, VaultProperties.VaultName, AccessPolicyUpdateKind.Replace, accessPolicyParameters);
                //force refresh of clients to get a new access token to refresh authority.
                _mgmtClient = null;
                _certClient = null;
            }
            catch (Exception ex)
            {
                logger.LogError("Unable to set access policy on Vault", ex);
                throw;
            }
        }
    }
}

