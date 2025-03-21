
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    [Job(JobTypes.INVENTORY)]
    public class Inventory : AzureKeyVaultJob<Inventory>, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver)
        {
            PamSecretResolver = resolver;
            logger = LogHandler.GetClassLogger<Inventory>();
        }
        
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate callBack)
        {
            logger.LogDebug($"Begin Inventory...");

            InitializeStore(config);

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                logger.LogTrace($"Making Request to get certificates from vault at {VaultProperties.VaultURL}");

                inventoryItems = AzClient.GetCertificatesAsync().Result?.ToList();

                logger.LogTrace($"Found {inventoryItems.Count()} Total Certificates in Azure Key Vault.");
            }

            catch (Exception ex)
            {
                logger.LogTrace($"an error occured when performing inventory: {ex.Message}");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ex.Message
                };
            }
            // upload to CMS
            callBack.DynamicInvoke(inventoryItems);

            return new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = config.JobHistoryId,
                FailureMessage = string.Empty
            };
        }
    }
}

