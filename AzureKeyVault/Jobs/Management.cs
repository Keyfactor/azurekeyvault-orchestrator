
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager.KeyVault;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    [Job(JobTypes.MANAGEMENT)]
    public class Management : AzureKeyVaultJob<Management>, IManagementJobExtension
    {
        public Management(IPAMSecretResolver resolver)
        {
            PamSecretResolver = resolver;
            Logger = LogHandler.GetClassLogger<Management>();
        }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            Logger.LogDebug($"Begin Management job");

            InitializeStore(config);

            Logger.LogTrace($"raw entry parameters from command: {JsonConvert.SerializeObject(config.JobProperties)}");

            JobResult complete = new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = "Invalid Management Operation"
            };

            string tagsJSON;
            bool preserveTags;
            bool nonExportable;

            Logger.LogTrace("parsing entry parameters.. ");

            tagsJSON = config.JobProperties.ContainsKey(EntryParameters.TAGS)
                ? config.JobProperties[EntryParameters.TAGS] as string ?? string.Empty
                : string.Empty;
            preserveTags = config.JobProperties.ContainsKey(EntryParameters.PRESERVE_TAGS)
                ? config.JobProperties[EntryParameters.PRESERVE_TAGS] as bool? ?? true
                : true;
            nonExportable = config.JobProperties.ContainsKey(EntryParameters.NON_EXPORTABLE)
                ? config.JobProperties[EntryParameters.NON_EXPORTABLE] as bool? ?? false
                : false;

            switch (config.OperationType)
            {
                case CertStoreOperationType.Create:
                    Logger.LogDebug($"Begin Management > Create...");
                    complete = PerformCreateVault(config.JobHistoryId).GetAwaiter().GetResult();
                    break;
                case CertStoreOperationType.Add:
                    Logger.LogDebug($"Begin Management > Add...");

                    complete = PerformAddition(config.JobCertificate.Alias, config.JobCertificate.PrivateKeyPassword, config.JobCertificate.Contents, tagsJSON, config.JobHistoryId, config.Overwrite, preserveTags, nonExportable);
                    break;
                case CertStoreOperationType.Remove:
                    Logger.LogDebug($"Begin Management > Remove...");
                    complete = PerformRemoval(config.JobCertificate.Alias, tagsJSON, config.JobHistoryId);
                    break;
            }

            return complete;
        }

        #region Create

        protected async Task<JobResult> PerformCreateVault(long jobHistoryId)
        {
            KeyVaultResource createVaultResult;
            var jobResult = new JobResult() { JobHistoryId = jobHistoryId, Result = OrchestratorJobStatusJobResult.Failure };
            try
            {
                createVaultResult = await AzClient.CreateVault();
            }
            catch (Exception ex)
            {
                jobResult.FailureMessage = ex.Message;
                return jobResult;
            }

            if (createVaultResult.Id != string.Empty && createVaultResult.Id.ToString().Contains(VaultProperties.VaultName))
            {
                jobResult.Result = OrchestratorJobStatusJobResult.Success;
            }
            else
            {
                jobResult.FailureMessage = "The creation of the Azure Key Vault failed for an unknown reason. Check your job parameters and ensure permissions are correct.";
            }

            return jobResult;
        }

        #endregion

        #region Add
        protected virtual JobResult PerformAddition(string alias, string pfxPassword, string entryContents, string tagsJSON, long jobHistoryId, bool overwrite, bool preserveTags, bool nonExportable)
        {
            var complete = new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = jobHistoryId };

            var tagDict = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(tagsJSON))
            {
                if (!tagsJSON.IsValidJson())
                {
                    Logger.LogError($"the entry parameter provided for Certificate Tags: \" {tagsJSON} \", does not seem to be valid JSON.");
                    throw new Exception($"the string \" {tagsJSON} \" is not a valid json string. Please enter a valid json string for CertificateTags in the entry parameter or leave empty for no tags to be applied.");
                }
                else
                {
                    tagDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(tagsJSON);
                }
            }

            if (!string.IsNullOrWhiteSpace(pfxPassword)) // This is a PFX Entry
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    complete.FailureMessage = "You must supply an alias for the certificate.";
                    return complete;
                }

                try
                {
                    var existingTags = new Dictionary<string, string>();
                    Logger.LogTrace($"checking for an existing cert with the alias {alias}");
                    var existing = AzClient.GetCertificate(alias).GetAwaiter().GetResult();
                    if (existing != null)
                    {
                        Logger.LogTrace($"there is an existing cert..");

                        existingTags = existing?.Properties.Tags as Dictionary<string, string> ?? new Dictionary<string, string>();

                        Logger.LogTrace("existing cert tags: ");
                        if (!existingTags.Any()) Logger.LogTrace("(none)");

                        foreach (var tag in existingTags)
                        {
                            Logger.LogTrace(tag.Key + " : " + tag.Value);
                        }
                    }

                    // if overwrite is unchecked, check for an existing cert first
                    if (!overwrite)
                    {
                        if (existing != null)
                        {
                            var message = $"A certificate named {alias} already exists and the overwrite checkbox was unchecked.  No action was taken.";
                            Logger.LogWarning(message);
                            complete.Result = OrchestratorJobStatusJobResult.Warning;
                            complete.FailureMessage = message;
                            return complete;
                        }
                    }
                    else if (preserveTags)
                    { // if preserveTags is true, we want to get the existing tags before replacing the cert
                        foreach (var existingTag in existingTags)
                        {
                            if (!tagDict.ContainsKey(existingTag.Key)) // if it's not being overwritten by what was provided..
                            {
                                tagDict[existingTag.Key] = existingTag.Value; // then we include it
                            }
                        }
                    }

                    var cert = AzClient.ImportCertificateAsync(alias, entryContents, pfxPassword, tagDict, nonExportable).GetAwaiter().GetResult();

                    // Ensure the return object has a AKV version tag, and Thumbprint
                    if (!string.IsNullOrEmpty(cert.Properties.Version) &&
                        !string.IsNullOrEmpty(string.Concat(cert.Properties.X509Thumbprint.Select(i => i.ToString("X2"))))
                    )
                    {
                        complete.Result = OrchestratorJobStatusJobResult.Success;
                    }
                    else
                    {
                        // uploadCollection is either not null or an exception was thrown.
                        complete.FailureMessage = $"Unable to add {alias} to {ExtensionName}. Check your network connection, ensure the password is correct, and that your API connection information is correct.";
                    }
                }
                catch (Exception ex)
                {
                    complete.FailureMessage = $"An error occurred while adding {alias} to {ExtensionName}: " + ex.Message;

                    if (ex.InnerException != null)
                        complete.FailureMessage += " - " + ex.InnerException.Message;
                }
            }
            else  // Non-PFX
            {
                complete.FailureMessage = "Certificate to add must be in a .PFX file format.";
            }

            return complete;
        }

        #endregion

        #region Remove

        protected virtual JobResult PerformRemoval(string alias, string tagsJSON, long jobHistoryId)
        {
            JobResult complete = new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = jobHistoryId };

            if (string.IsNullOrWhiteSpace(alias))
            {
                complete.FailureMessage = "You must supply an alias for the certificate.";
                return complete;
            }

            try
            {
                var result = AzClient.DeleteCertificateAsync(alias).GetAwaiter().GetResult();

                if (result.Value.Name == alias)
                {
                    complete.Result = OrchestratorJobStatusJobResult.Success;
                }
                else
                {
                    complete.FailureMessage = $"Unable to remove {alias} from {ExtensionName}. Check your network connection, ensure the password is correct, and that your API connection information is correct.";
                }
            }

            catch (Exception ex)
            {
                complete.FailureMessage = $"An error occurred while removing {alias} from {ExtensionName}: " + ex.Message;
            }
            return complete;
        }

        #endregion
    }
}


