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
        private Uri AzureCloudEndpoint
        {
            get
            {
                switch (VaultProperties.AzureCloud?.ToLower())
                {

                    case "china":
                        return AzureAuthorityHosts.AzureChina;
                    //case "germany":
                    //    return AzureAuthorityHosts.AzureGermany; // germany is no longer a valid azure authority host as of 2021
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
                    logger.LogTrace("getting previously initialized certificate client");
                    return _certClient;
                }
                logger.LogTrace("initializing new instance of client.");
                TokenCredential cred;

                // check to see if they have selected to use an Azure Managed Identity for authentication.

                if (this.VaultProperties.UseAzureManagedIdentity)
                {
                    logger.LogTrace("Entering the managed identity workflow");

                    var credentialOptions = new DefaultAzureCredentialOptions { AuthorityHost = AzureCloudEndpoint, AdditionallyAllowedTenants = { "*" } };

                    if (!string.IsNullOrEmpty(this.VaultProperties.ClientId)) // are they using a user assigned identity instead of a system assigned one (default)?
                    {
                        logger.LogTrace("client ID provided, so it is a user assigned managed identity (instead of system assigned)");
                        credentialOptions.ManagedIdentityClientId = VaultProperties.ClientId;
                    }
                    cred = new DefaultAzureCredential(credentialOptions);
                }
                else
                {
                    logger.LogTrace("Using a service principal to authenticate, generating the credentials");
                    cred = new ClientSecretCredential(VaultProperties.TenantId, VaultProperties.ClientId, VaultProperties.ClientSecret, new ClientSecretCredentialOptions() { AuthorityHost = AzureCloudEndpoint, AdditionallyAllowedTenants = { "*" } });
                    logger.LogTrace("generated credentials");
                }
                var certClientOptions = new CertificateClientOptions() { DisableChallengeResourceVerification = true }; // without this, requests fail when running behind a proxy https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/keyvault/TROUBLESHOOTING.md#incorrect-challenge-resource 
                _certClient = new CertificateClient(new Uri(VaultProperties.VaultURL), credential: cred, certClientOptions);

                return _certClient;
            }
        }
        protected CertificateClient _certClient { get; set; }

        internal protected virtual ArmClient getArmClient(string tenantId)
        {
            TokenCredential credential;
            var credentialOptions = new DefaultAzureCredentialOptions { AuthorityHost = AzureCloudEndpoint, AdditionallyAllowedTenants = { "*" } };
            if (this.VaultProperties.UseAzureManagedIdentity)
            {
                logger.LogTrace("getting management client for a managed identity");
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
                logger.LogTrace($"getting credentials for a service principal identity with id {VaultProperties.ClientId} in Azure Tenant {credentialOptions.TenantId}");
                credential = new ClientSecretCredential(tenantId, VaultProperties.ClientId, VaultProperties.ClientSecret, credentialOptions);
                logger.LogTrace("got credentials for service principal identity");
            }

            _mgmtClient = new ArmClient(credential, VaultProperties.SubscriptionId, new ArmClientOptions() { });
            logger.LogTrace("created management client");
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
            logger.LogTrace("calling method to delete certificate");
            return await CertClient.StartDeleteCertificateAsync(certName);
        }

        public virtual async Task<KeyVaultResource> CreateVault()
        {
            try
            {
                logger.LogTrace($"Begin create vault in Subscription {VaultProperties.SubscriptionId} with storepath = {VaultProperties.StorePath}");

                logger.LogTrace($"getting subscription info for provided subscription id {VaultProperties.SubscriptionId}");

                SubscriptionResource subscription = KvManagementClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(VaultProperties.SubscriptionId));
                ResourceGroupResource resourceGroup = subscription.GetResourceGroup(VaultProperties.ResourceGroupName);

                AzureLocation loc;

                var vaults = resourceGroup.GetKeyVaults();
                if (string.IsNullOrEmpty(VaultProperties.VaultRegion))
                {
                    try
                    {
                        logger.LogTrace($"no Vault Region location specified for new Vault, Getting available regions for resource group {resourceGroup.Data.Name}.");
                        var locOptions = await resourceGroup.GetAvailableLocationsAsync();
                        logger.LogTrace($"got location options for subscription {subscription.Data.SubscriptionId}", locOptions);
                        loc = locOptions.Value.FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"error retrieving default Azure Location: {ex.Message}");
                        throw;
                    }
                }
                else
                {
                    loc = new AzureLocation(VaultProperties.VaultRegion);
                }

                var skuType = VaultProperties.PremiumSKU ? KeyVaultSkuName.Premium : KeyVaultSkuName.Standard;

                var content = new KeyVaultCreateOrUpdateContent(loc, new KeyVaultProperties(new Guid(VaultProperties.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, skuType)));

                var newVault = await vaults.CreateOrUpdateAsync(WaitUntil.Completed, VaultProperties.VaultName, content);

                return newVault.Value;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error when trying to create Azure Keyvault {ex.Message}");
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
                    RecoverDeletedCertificateOperation recovery = await CertClient.StartRecoverDeletedCertificateAsync(certName);
                    recovery.WaitForCompletion();
                }
                logger.LogTrace("begin creating x509 certificate from contents.");
                var bytes = Convert.FromBase64String(contents);

                var x509Collection = new X509Certificate2Collection();//(bytes, pfxPassword, X509KeyStorageFlags.Exportable);

                x509Collection.Import(bytes, pfxPassword, X509KeyStorageFlags.Exportable);

                var certWithKey = x509Collection.Export(X509ContentType.Pkcs12);


                logger.LogTrace($"importing created x509 certificate named {1}", certName);
                logger.LogTrace($"There are {x509Collection.Count} certificates in the chain.");
                var cert = await CertClient.ImportCertificateAsync(new ImportCertificateOptions(certName, certWithKey));
                
                return cert;
            }
            catch (Exception ex)
            {
                logger.LogError($"There was an error importing the certificate: {ex.Message}");
                throw;
            }
        }

        public virtual async Task<KeyVaultCertificateWithPolicy> GetCertificate(string alias)
        {
            KeyVaultCertificateWithPolicy cert = null;
            logger.LogTrace($"Attempting to retreive certificate with alias {alias} from the KeyVault.");

            try { cert = await CertClient.GetCertificateAsync(alias); }
            catch (RequestFailedException rEx)
            {
                if (rEx.ErrorCode == "CertificateNotFound")
                {
                    // the request was successful, the cert does not exist.
                    logger.LogTrace($"The certificate with alias {alias} was not found: {rEx.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error retreiving certificate with alias {alias}.  {ex.Message}", ex);
                throw;
            }

            return cert;
        }

        public virtual async Task<IEnumerable<CurrentInventoryItem>> GetCertificatesAsync()
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            AsyncPageable<CertificateProperties> inventory = null;
            try
            {
                logger.LogTrace("calling GetPropertiesOfCertificates() on the Certificate Client");
                inventory = CertClient.GetPropertiesOfCertificatesAsync();

                logger.LogTrace($"got a pageable response");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error performing inventory.  {ex.Message}", ex);
                throw;
            }

            logger.LogTrace("iterating over result pages for complete list..");

            var fullInventoryList = new List<CertificateProperties>();
            var failedCount = 0;
            Exception innerException = null;

            await foreach (var cert in inventory)
            {
                logger.LogTrace($"adding cert with ID: {cert.Id} to the list.");
                fullInventoryList.Add(cert); // convert to list from pages
            }

            logger.LogTrace($"compiled full inventory list of {fullInventoryList.Count()} certificate(s)");

            foreach (var certificate in fullInventoryList)
            {
                logger.LogTrace($"getting details for the individual certificate with id: {certificate.Id} and name: {certificate.Name}");
                try
                {
                    var cert = await CertClient.GetCertificateAsync(certificate.Name);
                    logger.LogTrace($"got certificate details");

                    inventoryItems.Add(new CurrentInventoryItem()
                    {
                        Alias = cert.Value.Name,
                        PrivateKeyEntry = true,
                        ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                        UseChainLevel = true,
                        Certificates = new List<string>() { Convert.ToBase64String(cert.Value.Cer) }
                    });
                }
                catch (Exception ex)
                {
                    failedCount++;
                    innerException = ex;
                    logger.LogError($"Failed to retreive details for certificate {certificate.Name}.  Exception: {ex.Message}");
                    // continuing with inventory instead of throwing, in case there's an issue with a single certificate
                }
            }

            if (failedCount == fullInventoryList.Count())
            {
                throw new Exception("Unable to retreive details for certificates.", innerException);
            }

            if (failedCount > 0)
            {
                logger.LogWarning($"{failedCount} of {fullInventoryList.Count()} certificates were not able to be retreieved.  Please review the errors.");
            }

            return inventoryItems;
        }

        public virtual (List<string>, List<string>) GetVaults()
        {
            var vaultNames = new List<string>();
            var warnings = new List<string>();
            var searchSubscription = string.Empty;
            var searchTenantId = string.Empty;

            try
            {
                if (VaultProperties.TenantIdsForDiscovery == null || VaultProperties.TenantIdsForDiscovery.Count() < 1)
                {
                    throw new Exception("no tenant ID's provided.");
                }
                VaultProperties.TenantIdsForDiscovery.ForEach(tenantId =>
                {
                    searchTenantId = tenantId;
                    logger.LogTrace($"getting ARM client for tenantId {tenantId}");

                    var mgmtClient = getArmClient(tenantId);

                    logger.LogTrace($"getting all available subscriptions in tenant with ID {tenantId}");
                    var allSubs = mgmtClient.GetSubscriptions();

                    logger.LogTrace($"got {allSubs.Count()} subscriptions");

                    foreach (var sub in allSubs)
                    {
                        searchSubscription = sub.Id.SubscriptionId;
                        logger.LogTrace($"searching for vaults in subscription with ID {sub.Data.SubscriptionId}");
                        var vaults = sub.GetKeyVaults();
                        logger.LogTrace($"found {vaults.Count()} vaults.");

                        foreach (var vault in vaults)
                        {
                            var splitId = vault.Id.ToString().Split("/", StringSplitOptions.RemoveEmptyEntries);
                            // example resource identifier: /subscriptions/b3114ff1-bb92-45b6-9bd6-e4a1eed8c91e/resourceGroups/azure_sentinel_evaluation/providers/Microsoft.KeyVault/vaults/jv2-vault
                            var subId = splitId[1];
                            var resourceGroupName = splitId[3];
                            var vaultName = splitId.Last();
                            var vaultStorePath = $"{subId}:{resourceGroupName}:{vaultName}";
                            logger.LogTrace($"found keyvault, using storepath {vaultStorePath}");
                            vaultNames.Add($"{subId}:{resourceGroupName}:{vaultName}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogTrace($"Exception thrown during discovery. Log warning and continue.");
                var warning = $"Exception thrown performing discovery on tenantId {searchTenantId} and subscription ID {searchSubscription}.  Exception message: {ex.Message}";

                logger.LogWarning(warning);
                warnings.Add(warning);
                //throw;
            }
            return (vaultNames, warnings);
        }
    }
}

