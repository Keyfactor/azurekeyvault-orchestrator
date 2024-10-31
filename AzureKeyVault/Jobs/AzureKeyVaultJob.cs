// Copyright 2023 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System;
using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public abstract class AzureKeyVaultJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => AzureKeyVaultConstants.STORE_TYPE_NAME;

        internal protected virtual AzureClient AzClient { get; set; }
        internal protected virtual AkvProperties VaultProperties { get; set; }

        internal protected IPAMSecretResolver PamSecretResolver { get; set; }
        internal protected ILogger logger { get; set; }

        public void InitializeStore(dynamic config)
        {
            try
            {
                VaultProperties = new AkvProperties();
                if (config.GetType().GetProperty("ClientMachine") != null) // Discovery job
                    VaultProperties.TenantId = config.ClientMachine;

                logger.LogTrace($"Got tenant ID {VaultProperties.TenantId} from ClientMachine field.");
                // ClientId can be omitted for system assigned managed identities, required for user assigned or service principal auth
                VaultProperties.ClientId = PAMUtilities.ResolvePAMField(PamSecretResolver, logger, "Server UserName", config.ServerUsername);

                logger.LogTrace($"Using client id {VaultProperties.ClientId}");

                // ClientSecret can be omitted for managed identities, required for service principal auth
                VaultProperties.ClientSecret = PAMUtilities.ResolvePAMField(PamSecretResolver, logger, "Server Password", config.ServerPassword);

                if (VaultProperties.ClientSecret == null)
                {
                    logger.LogTrace("No client secret provided, assuming Managed Identity authentication");
                }
                else
                {
                    logger.LogTrace("client secret provided, assuming Service Principal authentication");
                }

                if (config.GetType().GetProperty("CertificateStoreDetails") != null) // anything except a discovery job
                {
                    logger.LogTrace("CertificateStoreDetails is not empty, (non-Discovery job) applying values..");
                    VaultProperties.StorePath = config.CertificateStoreDetails?.StorePath;
                    dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());

                    // get the values from the storepath field.  format is <subscription id>:<resource group name>:<vault name>
                    var storePathFields = VaultProperties.StorePath.Split(":");

                    if (storePathFields.Length == 3)
                    { //using the latest (3 fields)
                        logger.LogTrace("storepath split by `:` into 3 parts. subscription ID: {{subscription id}}:{{resource group name}}:{{vault name}}.");
                        VaultProperties.SubscriptionId = storePathFields[0].Trim();
                        VaultProperties.ResourceGroupName = storePathFields[1].Trim();
                        VaultProperties.VaultName = storePathFields[2]?.Trim();
                    }

                    // support legacy store path <subscription id>:<vault name> 
                    if (storePathFields.Length == 2)
                    { // using previous version (2 fields)
                        logger.LogTrace($"storepath split by `:` into 2 parts.  {storePathFields}.  Using {{subscription id}}:{{vault name}} format.");
                        VaultProperties.SubscriptionId = storePathFields[0].Trim();
                        VaultProperties.VaultName = storePathFields[1].Trim();
                        VaultProperties.ResourceGroupName = properties.ResourceGroupName;
                    }

                    // support legacy store path <full azure resource identifier>
                    // - example: /subscriptions/b3114ff1-bb92-45b6-9bd6-e4a1eed8c91e/resourceGroups/azure_sentinel_evaluation/providers/Microsoft.KeyVault/vaults/jv2-vault            
                    if (storePathFields.Length == 1)
                    {
                        var legacyPathComponents = VaultProperties.StorePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (legacyPathComponents.Length == 8) // they are using the full resource path
                        {
                            logger.LogTrace($"full resource identifier provided {storePathFields}.  Using {{subscription id}}:{{vault name}} format.");
                            VaultProperties.SubscriptionId = legacyPathComponents[1];
                            VaultProperties.ResourceGroupName = legacyPathComponents[3];
                            VaultProperties.VaultName = legacyPathComponents[7];
                        }
                    }

                    logger.LogTrace($"Parsed storepath: Subscription ID: {VaultProperties.SubscriptionId}, ResourceGroupName: {VaultProperties.ResourceGroupName}, VaultName: {VaultProperties.VaultName}");

                    VaultProperties.SubscriptionId = properties.SubscriptionId ?? VaultProperties.SubscriptionId;
                    VaultProperties.ResourceGroupName = !string.IsNullOrEmpty(properties.ResourceGroupName) ? properties.ResourceGroupName : VaultProperties.ResourceGroupName;
                    VaultProperties.VaultName = properties.VaultName ?? VaultProperties.VaultName; // check the field in case of legacy paths.                    
                    VaultProperties.TenantId = !string.IsNullOrEmpty(VaultProperties.TenantId) ? VaultProperties.TenantId : config.CertificateStoreDetails?.ClientMachine; // Client Machine could be null in the case of managed identity.  That's ok.
                    VaultProperties.AzureCloud = !string.IsNullOrEmpty(properties.AzureCloud) ? properties.AzureCloud : VaultProperties.AzureCloud;
                    VaultProperties.PrivateEndpoint = !string.IsNullOrEmpty(properties.PrivateEndpoint) ? properties.PrivateEndpoint : VaultProperties.PrivateEndpoint;

                    string skuType = !string.IsNullOrEmpty(properties.SkuType) ? properties.SkuType : null;
                    VaultProperties.PremiumSKU = skuType?.ToLower() == "premium";
                    VaultProperties.VaultRegion = !string.IsNullOrEmpty(properties.VaultRegion) ? properties.VaultRegion : VaultProperties.VaultRegion;
                    VaultProperties.VaultRegion = VaultProperties.VaultRegion?.ToLower();
                }
                else // discovery job : Discovery only works on the Global Public Azure cloud because we do not have a way to pass the Azure Cloud instance value during a discovery job.
                {
                    logger.LogTrace("Discovery job - getting tenant ids from directories to search field.");
                    VaultProperties.TenantIdsForDiscovery = new List<string>();
                    var dirs = config.JobProperties?["dirs"] as string;
                    logger.LogTrace($"Directories to search: {dirs}");

                    if (!string.IsNullOrEmpty(dirs))
                    {
                        // parse the list of tenant ids to perform discovery on                                                                
                        VaultProperties.TenantIdsForDiscovery.AddRange(dirs.Split(','));
                    }
                    else
                    {
                        // if it is empty, we use the default provided Tenant Id only
                        VaultProperties.TenantIdsForDiscovery.Add(VaultProperties.TenantId);
                    }

                    VaultProperties.TenantIdsForDiscovery.ForEach(tId => tId = tId.Trim());
                    VaultProperties.TenantId = VaultProperties.TenantId ?? VaultProperties.TenantIdsForDiscovery[0]; // if VaultProperties.TenantId is null here, they are using Managed Identity auth and no TenantID field; 
                }
                AzClient ??= new AzureClient(VaultProperties);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error initializing store: {ex.Message}");
                throw;
            }

            logger.LogTrace($"Initialization complete, configuration values set.");
        }
    }
}

