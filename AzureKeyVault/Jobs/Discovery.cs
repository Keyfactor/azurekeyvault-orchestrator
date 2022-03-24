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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    [Job(JobTypes.DISCOVERY)]
    public class Discovery : AzureKeyVaultJob<Discovery>, IDiscoveryJobExtension
    {
        ILogger logger = LogHandler.GetClassLogger<Discovery>();
        public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            logger.LogDebug($"Begin Discovery...");

            SubscriptionId = config.ClientMachine;
            DirectoryId = config.ServerUsername.Split()[0]; //username should contain "<tenantId guid> <app id guid>"
            ApplicationId = config.ServerUsername.Split()[1];
            ClientSecret = config.ServerPassword;

            var complete = new JobResult() { JobHistoryId = config.JobHistoryId, Result = OrchestratorJobStatusJobResult.Failure };
            var keyVaults = new List<string>();

            // Server credentials
            AzClient ??= new AzureClient(ApplicationId, DirectoryId, ClientSecret);

            //string[] subscriptionIds = config.JobProperties.GetValueOrDefault("dirs")
            //    .ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var result = AzClient.GetVaults(SubscriptionId).Result;
                foreach (var keyVault in result.Vaults)
                    keyVaults.Add(keyVault.Id);

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

    public class _DiscoveryResult
    {
        [JsonProperty("value")]
        public List<_AKV_Location> Vaults { get; set; }
    }

    public class _AKV_Location
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("location")]
        public string location { get; set; }

        [JsonProperty("tags")]
        public _Tags Tags { get; set; }
    }

    public class _Tags
    {
        public List<string> Values { get; set; }
    }
}
