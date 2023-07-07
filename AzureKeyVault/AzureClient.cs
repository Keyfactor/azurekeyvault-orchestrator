// Copyright 2023 Keyfactor
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
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Certificates;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AzureClient
    {
        internal protected virtual AkvProperties VaultProperties { get; set; }

        private Uri AzureCloudEndpoint {
            get {
                switch (VaultProperties.AzureCloud.ToLower()) {

                    case "china":
                        return AzureAuthorityHosts.AzureChina;                        
                    case "germany":
                        return AzureAuthorityHosts.AzureGermany;                        
                    case "government":
                        return AzureAuthorityHosts.AzureGovernment;
                    default:
                        return AzureAuthorityHosts.AzurePublicCloud;
                }
            }
        }
        ILogger logger { get; set; }

        private protected virtual CertificateClient CertClient
        {
            get
            {
                if (_certClient != null)
                {
                    return _certClient;
                }

                TokenCredential cred;

                // check to see if they have selected to use an Azure Managed Identity for authentication.

                if (this.VaultProperties.UseAzureManagedIdentity)
                {
                    var credentialOptions = new DefaultAzureCredentialOptions { AuthorityHost = AzureCloudEndpoint };

                    if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // are they using a user assigned identity instead of a system assigned one (default)?
                    {
                        credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                    }
                    cred = new DefaultAzureCredential(credentialOptions);
                }
                else
                {
                    cred = new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ClientId, VaultProperties.ClientSecret);
                }

                _certClient = new CertificateClient(new Uri(VaultProperties.VaultURL), credential: cred);

                return _certClient;
            }
        }
        protected CertificateClient _certClient { get; set; }

        internal protected virtual ArmClient KvManagementClient
        {
            get
            {
                if (_mgmtClient != null)
                {
                    return _mgmtClient;
                }

                // var subId = VaultProperties.SubscriptionId ?? VaultProperties.StorePath.Split("/")[2];
                // var creds = SdkContext.AzureCredentialsFactory.FromServicePrincipal(VaultProperties.ApplicationId, VaultProperties.ClientSecret, VaultProperties.TenantId, AzureEnvironment.AzureGlobalCloud);
                //NOTE: creating a certificate store from the platform is currently only supported for Azure GlobalCloud customers.

                TokenCredential credential;

                if (this.VaultProperties.UseAzureManagedIdentity)
                {
                    var credentialOptions = new DefaultAzureCredentialOptions { AuthorityHost = AzureCloudEndpoint };


                    if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // they have selected a managed identity and provided a client ID, so it is a user assigned identity
                    {
                        credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                    }
                    credential = new DefaultAzureCredential(credentialOptions);
                }
                else
                {
                    credential = new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ClientId, VaultProperties.ClientSecret);
                }

                _mgmtClient = new ArmClient(credential);
                return _mgmtClient;
            }
        }
        protected virtual ArmClient _mgmtClient { get; set; }

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

        public virtual async Task<KeyVaultResource> CreateVault()
        {
            try
            {
                logger.LogInformation("Begin create vault...");

                SubscriptionResource subscription = await KvManagementClient.GetDefaultSubscriptionAsync();
                ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
                ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(this.VaultProperties.ResourceGroupName);

                var vaults = resourceGroup.GetKeyVaults();


                //TODO: Create store type parameter for Azure Location.

                var loc = new AzureLocation(VaultProperties.VaultRegion); // pass property instead of hardcoded value after testing

                var skuType = VaultProperties.PremiumSKU ? KeyVaultSkuName.Premium : KeyVaultSkuName.Standard;

                var content = new KeyVaultCreateOrUpdateContent(loc, new KeyVaultProperties(new Guid(VaultProperties.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, skuType)));

                var newVault = await vaults.CreateOrUpdateAsync(WaitUntil.Completed, VaultProperties.VaultName, content); //this takes a bit of time to run

                return newVault.Value;
            }
            catch (Exception ex)
            {
                logger.LogError("Error when trying to create Azure Keyvault", ex);
                throw;
            }

        }

        public virtual async Task<KeyVaultCertificateWithPolicy> ImportCertificateAsync(string certName, string contents, string pfxPassword)
        {
            try
            {
                if (CertClient.GetDeletedCertificates().FirstOrDefault(i => i.Name == certName) != null)
                {
                    RecoverDeletedCertificateOperation recovery = await CertClient.StartRecoverDeletedCertificateAsync(certName);
                    recovery.WaitForCompletion();
                }

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
                logger.LogError("Error performing inventory", ex);
                throw;
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
            // discovery is currently only available for the Azure Public cloud.
            // We need a way to pass the Azure Cloud parameter as part of a discovery job to support other cloud instances.

            try
            {
                SubscriptionResource subscription = await KvManagementClient.GetDefaultSubscriptionAsync();
                ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
                var vaultNames = new List<string>();

                resourceGroups.ToList().ForEach(rg => // we go through all of the resource groups that the identity has access to
                {
                    var rgVaults = rg.GetKeyVaults().ToList();
                    vaultNames.AddRange(rgVaults.Select(v => subscription.Id + ":" + v.Data.Name));

                });
                return vaultNames;
            }
            catch (Exception ex)
            {
                logger.LogError("Error getting vaults.", ex);
                throw;
            }
        }
    }
}

