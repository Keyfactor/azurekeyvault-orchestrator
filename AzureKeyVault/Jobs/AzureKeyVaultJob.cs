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
        internal protected virtual string ResourceId { get; set; }
        internal protected virtual string ApiObjectId { get; set; }
        internal protected virtual string DirectoryId { get; set; }
        internal protected virtual string ClientSecret { get; set; }
        internal protected virtual string ApplicationId { get; set; }
        internal protected virtual string SubscriptionId { get; set; }
        internal protected virtual string VaultName { get; set; }
        internal protected virtual string ResourceGroupName { get; set; }

        internal protected string VaultURL => $"https://{VaultName}.vault.azure.net/";

        public void InitializeStore(ManagementJobConfiguration config)
        {
            ResourceId = config.CertificateStoreDetails.StorePath;
            SubscriptionId = ResourceId.Split('/')[2];

            DirectoryId = config.ServerUsername.Split(',')[0]; //username should contain "<tenantId guid> <app id guid>"
            ApplicationId = config.ServerUsername.Split(',')[1];
            ClientSecret = config.ServerPassword;

            dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());

            ResourceGroupName = properties.ResourceGroupName;
            VaultName = properties.VaultName;
            ApiObjectId = properties.APIObjectId;

            AzClient ??= new AzureClient(ApplicationId, DirectoryId, ClientSecret, VaultURL);
        }
        public void InitializeStore(InventoryJobConfiguration config)
        {
            ResourceId = config.CertificateStoreDetails.StorePath;
            SubscriptionId = config.CertificateStoreDetails.ClientMachine;

            DirectoryId = config.ServerUsername.Split()[0]; //username should contain "<tenantId guid> <app id guid>"
            ApplicationId = config.ServerUsername.Split()[1];
            ClientSecret = config.ServerPassword;

            dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());

            ResourceGroupName = properties.ResourceGroupName;
            VaultName = properties.VaultName;
            ApiObjectId = properties.APIObjectId;

            AzClient ??= new AzureClient(ApplicationId, DirectoryId, ClientSecret, VaultURL);
        }
    }
}

