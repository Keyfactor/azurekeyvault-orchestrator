
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
    [Job(JobTypes.DISCOVERY)]
    public class Discovery : AzureKeyVaultJob<Discovery>, IDiscoveryJobExtension
    {
        public Discovery(IPAMSecretResolver resolver)
        {
            PamSecretResolver = resolver;
            logger = LogHandler.GetClassLogger<Discovery>();
        }

        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            logger.LogDebug($"Begin Discovery job");
            InitializeStore(config);

            var complete = new JobResult() { JobHistoryId = config.JobHistoryId, Result = OrchestratorJobStatusJobResult.Failure };
            List<string> keyVaults, warnings;

            try
            {
                if (VaultProperties.TenantId == null) throw new MissingFieldException("Need to set Tenant Id value in directories to search for discovery jobs that use a system managed identity.");

                (keyVaults, warnings) = AzClient.GetVaults();
            }
            catch (Exception ex)
            {
                complete.FailureMessage = ex.Message;
                return complete;
            }

            // if there are no warnings return vaults and status of success
            if (warnings == null || !warnings.Any())
            {
                logger.LogTrace("discovery completed with no warnings or errors.");
                complete.Result = OrchestratorJobStatusJobResult.Success;
                complete.FailureMessage = $"Discovery job completed successfully.  Found {keyVaults?.Count() ?? 0} KeyVaults.";
            }

            // if there are warnings, but vaults were found, return Vaults and status of warn
            if (warnings?.Count() > 0 && keyVaults.Count() > 0)
            {
                logger.LogTrace("discovery completed with warnings.");
                complete.Result = OrchestratorJobStatusJobResult.Warning;
                complete.FailureMessage = $"Discovery job completed with errors.  Found {keyVaults?.Count() ?? 0} KeyVaults.\nThe following errors occurred: \n";
                complete.FailureMessage = complete.FailureMessage + string.Join('\n', warnings);
                if (complete.FailureMessage.Length > 4000)
                {
                    complete.FailureMessage = complete.FailureMessage.Substring(0, 3500) + "\n results truncated.  Please see the Orchestrator logs for more details.";
                }
            }

            // if there are warnings and no vaults were found, return status of error

            if (warnings?.Count() > 0 && keyVaults?.Count() == 0)
            {
                logger.LogTrace("discovery completed with errors and no vaults found (failed).");
                complete.Result = OrchestratorJobStatusJobResult.Failure;
                complete.FailureMessage = $"Discovery job failed with the following errors: \n";
                complete.FailureMessage = complete.FailureMessage + string.Join('\n', warnings);
            }

            // need to truncate failure message if it exceeds the max length of 4000
            if (complete.FailureMessage.Length > 4000)
            {
                logger.LogTrace($"Failure message length of {complete.FailureMessage.Length} exceeds the maximum of 4000; truncating.");
                complete.FailureMessage = complete.FailureMessage.Substring(0, 3500) + "\n results truncated.  Please see the Orchestrator logs for more details.";
            }

            sdr.Invoke(keyVaults);

            return complete;
        }
    }
}
