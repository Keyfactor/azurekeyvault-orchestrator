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

using Keyfactor.Orchestrators.Extensions;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public abstract class AzureKeyVaultJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => AzureKeyVaultConstants.STORE_TYPE_NAME;

        internal protected virtual AzureClient AzClient { get; set; }
        internal protected virtual AkvProperties VaultProperties { get; set; }

        public void InitializeStore(dynamic config)
        {
            VaultProperties = new AkvProperties();
            if (config.GetType().GetProperty("ClientMachine") != null)
                VaultProperties.SubscriptionId = config.ClientMachine;

            VaultProperties.TenantId = config.ServerUsername.Split()[0]; //username should contain "<tenantId guid> <app id guid> <object Id>"            
            VaultProperties.ApplicationId = config.ServerUsername.Split()[1];
            VaultProperties.ObjectId = config.ServerUsername.Split()[2];
            VaultProperties.ClientSecret = config.ServerPassword;

            if (config.GetType().GetProperty("CertificateStoreDetails") != null)
            {
                VaultProperties.StorePath = config.CertificateStoreDetails?.StorePath;
                dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());
                VaultProperties.ResourceGroupName = properties.ResourceGroupName;
                VaultProperties.VaultName = properties.VaultName;
                //VaultProperties.ObjectId = properties.APIObjectId;
            }
            AzClient ??= new AzureClient(VaultProperties);
        }        
    }
}

