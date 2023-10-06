// Copyright 2023 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

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
        private string ActiveTenantId { get; set; } // for searching across multiple tenants.

        ILogger logger { get; set; }


        private protected virtual CertificateClient CertClient
        {
            get
            {
                if (_certClient != null)
                {
                    logger.LogTrace("getting previously initialized certificate client");
                    return _certClient;
                }

                TokenCredential cred;

                // check to see if they have selected to use an Azure Managed Identity for authentication.

                if (this.VaultProperties.UseAzureManagedIdentity)
                {
                    logger.LogTrace("Entering the managed identity workflow");

                    var credentialOptions = new DefaultAzureCredentialOptions();

                    if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // are they using a user assigned identity instead of a system assigned one (default)?
                    {
                        logger.LogTrace("they provided client ID, so it is a user assigned managed identity (instead of system assigned)");
                        credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                    }
                    cred = new DefaultAzureCredential(credentialOptions);
                }
                else
                {
                    logger.LogTrace("They are using a service principal to authenticate, generating the credentials");
                    cred = new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ClientId, VaultProperties.ClientSecret);
                    logger.LogTrace("generated credentials", cred);
                }

                _certClient = new CertificateClient(new Uri(VaultProperties.VaultURL), credential: cred);

                return _certClient;
            }
        }
        protected CertificateClient _certClient { get; set; }

        internal protected virtual ArmClient getArmClient(string tenantId)
        {
            TokenCredential credential;

            if (this.VaultProperties.UseAzureManagedIdentity)
            {
                logger.LogTrace("getting management client for a managed identity");
                var credentialOptions = new DefaultAzureCredentialOptions();
                if (!string.IsNullOrEmpty(tenantId)) credentialOptions.TenantId = tenantId;

                if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // they have selected a managed identity and provided a client ID, so it is a user assigned identity
                {
                    logger.LogTrace("It is a user assigned managed identity");
                    credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                }
                credential = new DefaultAzureCredential(credentialOptions);
            }
            else
            {
                logger.LogTrace("getting credentials for a service principal identity");
                credential = new ClientSecretCredential(tenantId, VaultProperties.ClientId, VaultProperties.ClientSecret);
                logger.LogTrace("got credentials for service principal identity", credential);
            }

            _mgmtClient = new ArmClient(credential);
            logger.LogTrace("created management client", _mgmtClient);
            return _mgmtClient;
        }
        internal protected virtual ArmClient KvManagementClient
        {
            get
            {
                if (_mgmtClient != null)
                {
                    logger.LogTrace("getting previously initialized management client");
                    return _mgmtClient;
                }
                return getArmClient(VaultProperties.TenantId);

                // var subId = VaultProperties.SubscriptionId ?? VaultProperties.StorePath.Split("/")[2];
                // var creds = SdkContext.AzureCredentialsFactory.FromServicePrincipal(VaultProperties.ApplicationId, VaultProperties.ClientSecret, VaultProperties.TenantId, AzureEnvironment.AzureGlobalCloud);
                //NOTE: creating a certificate store from the platform is currently only supported for Azure GlobalCloud customers.

                //TokenCredential credential;

                //if (this.VaultProperties.UseAzureManagedIdentity)
                //{
                //    logger.LogTrace("getting management client for a managed identity");
                //    var credentialOptions = new DefaultAzureCredentialOptions();


                //    if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // they have selected a managed identity and provided a client ID, so it is a user assigned identity
                //    {
                //        logger.LogTrace("It is a user assigned managed identity");
                //        credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                //    }
                //    credential = new DefaultAzureCredential(credentialOptions);
                //}
                //else
                //{
                //    logger.LogTrace("getting credentials for a service principal identity");
                //    credential = new ClientSecretCredential(ActiveTenantId, VaultProperties.ClientId, VaultProperties.ClientSecret);
                //    logger.LogTrace("got credentials for service principal identity", credential);
                //}

                //_mgmtClient = new ArmClient(credential);
                //logger.LogTrace("created management client", _mgmtClient);
                //return _mgmtClient;
            }
        }
        protected virtual ArmClient _mgmtClient { get; set; }

        public AzureClient() { }
        public AzureClient(AkvProperties props)
        {
            VaultProperties = props;
            ActiveTenantId = props.TenantId;
            logger = LogHandler.GetClassLogger<AzureClient>();
        }

        public virtual async Task<DeleteCertificateOperation> DeleteCertificateAsync(string certName)
        {
            logger.LogTrace("calling method to delete certificate");
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

                var loc = new AzureLocation(VaultProperties.VaultRegion);

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
                logger.LogTrace("checking to see if the certificate exists and has been deleted");

                if (CertClient.GetDeletedCertificates().FirstOrDefault(i => i.Name == certName) != null)
                {
                    logger.LogTrace("certificate to import has been previously deleted, starting recovery operation.");
                    var recovery = await CertClient.StartRecoverDeletedCertificateAsync(certName);
                    recovery.WaitForCompletion();
                }

                logger.LogTrace("begin creating x509 certificate from contents.");
                var bytes = Convert.FromBase64String(contents);
                var x509 = new X509Certificate2(bytes, pfxPassword, X509KeyStorageFlags.Exportable);
                var certWithKey = x509.Export(X509ContentType.Pkcs12);
                logger.LogTrace($"importing created x509 certificate named {1}", certName);
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
                logger.LogTrace("calling GetPropertiesOfCertificates() on the Certificate Client", CertClient);
                inventory = CertClient.GetPropertiesOfCertificates();
                logger.LogTrace("got a response", inventory);
                var certQuantity = inventory.Count();
            }
            catch (Exception ex)
            {
                logger.LogError("Error performing inventory", ex);
                throw;
            }

            logger.LogTrace("retrieving each certificate from the response");
            foreach (var certificate in inventory)
            {
                logger.LogTrace("getting details for the individual certificate", certificate);
                var cert = await CertClient.GetCertificateAsync(certificate.Name);
                logger.LogTrace("got certificate response", cert);

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

        public virtual List<string> GetVaults()
        {
            var vaultNames = new List<string>();
            try
            {
                if (VaultProperties.TenantIdsForDiscovery == null || VaultProperties.TenantIdsForDiscovery.Count() < 1)
                {
                    throw new Exception("no tenant ID's provided.");
                }
                VaultProperties.TenantIdsForDiscovery.ForEach(tenantId =>
                {
                    logger.LogTrace($"getting ARM client for tenantId {tenantId}");

                    var mgmtClient = getArmClient(tenantId);

                    logger.LogTrace($"getting all available subscriptions in tenant with ID {tenantId}");
                    var allSubs = mgmtClient.GetSubscriptions();

                    logger.LogTrace($"got {allSubs.Count()} subscriptions");

                    foreach (var sub in allSubs) {
                        logger.LogTrace($"searching for vaults in subscription with ID {sub.Data.SubscriptionId}");
                        var vaults = sub.GetKeyVaults();
                        logger.LogTrace($"found {vaults.Count()} vaults.");
                        vaultNames.AddRange(vaults.Select(v => v.Id.ToString()));
                    }                    
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

