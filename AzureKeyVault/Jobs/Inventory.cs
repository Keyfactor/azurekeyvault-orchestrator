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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    [Job(JobTypes.INVENTORY)]
    public class Inventory : AzureKeyVaultJob<Inventory>, IInventoryJobExtension
    {
        ILogger logger = LogHandler.GetClassLogger<Inventory>();
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate callBack)
        {
            logger.LogDebug($"Begin Inventory...");

            InitializeStore(config);

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                logger.LogDebug($"Making Request for {0}...", VaultProperties.VaultURL);

                inventoryItems = AzClient.GetCertificatesAsync().Result?.ToList();

                logger.LogDebug($"Found {inventoryItems.Count()} Total Certificates in Azure Key Vault.");
            }

            catch (Exception ex)
            {
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

