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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    [Job(JobTypes.DISCOVERY)]
    public class Discovery : AzureKeyVaultJob<Discovery>, IDiscoveryJobExtension    {
        

        public Discovery(IPAMSecretResolver resolver)
        {
            PamSecretResolver = resolver;
            logger = LogHandler.GetClassLogger<Discovery>();
        }

        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            logger.LogDebug($"Begin Discovery...");
            InitializeStore(config);

            var complete = new JobResult() { JobHistoryId = config.JobHistoryId, Result = OrchestratorJobStatusJobResult.Failure };
            
            List<string> keyVaults;

            try
            {
                if (VaultProperties.TenantId == null) throw new MissingFieldException("Need to set Tenant Id value in directories to search for discovery jobs that use a system managed identity.");

                keyVaults = AzClient.GetVaults().Result;
            }
            catch (Exception ex)
            {
                complete.FailureMessage = ex.Message;
                return complete;
                throw;
            }

            sdr.Invoke(keyVaults);

            complete.Result = OrchestratorJobStatusJobResult.Success;
            return complete;
        }
    }   
}
