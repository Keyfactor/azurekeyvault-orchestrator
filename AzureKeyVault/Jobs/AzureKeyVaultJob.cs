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
                if (config.GetType().GetProperty("ClientMachine") != null)
                    VaultProperties.TenantId = config.ClientMachine;

                // ClientId can be omitted for system assigned managed identities, required for user assigned or service principal auth
                VaultProperties.ClientId = PAMUtilities.ResolvePAMField(PamSecretResolver, logger, "Server UserName", config.ServerUsername);

                // ClientSecret can be omitted for managed identities, required for service principal auth
                VaultProperties.ClientSecret = PAMUtilities.ResolvePAMField(PamSecretResolver, logger, "Server Password", config.ServerPassword);

                if (config.GetType().GetProperty("CertificateStoreDetails") != null)
                {
                    VaultProperties.StorePath = config.CertificateStoreDetails?.StorePath;
                    dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());
                    VaultProperties.TenantId = config.CertificateStoreDetails?.ClientMachine != null ? config.CertificateStoreDetails?.ClientMachine : VaultProperties.TenantId;
                    VaultProperties.TenantId = VaultProperties.TenantId ?? properties.dirs;
                    VaultProperties.ResourceGroupName = properties.ResourceGroupName;
                    VaultProperties.VaultName = properties.VaultName;
                    VaultProperties.PremiumSKU = properties.SkuType == "premium";
                    VaultProperties.VaultRegion = properties.VaultRegion ?? "eastus";
                    VaultProperties.VaultRegion = VaultProperties.VaultRegion.ToLower();
                }
                AzClient ??= new AzureClient(VaultProperties);
            }
            catch (Exception ex) {
                logger.LogError("Error initializing store", ex.Message);
                throw;
            }
        }        
    }
}

