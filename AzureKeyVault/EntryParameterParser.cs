
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    /// <summary>
    /// Declares one entry parameter that a job expects to find in Command's
    /// JobProperties, along with how to treat it when it's absent.
    /// </summary>
    public class EntryParameterDefinition
    {
        public string Name { get; }
        public bool Critical { get; }
        public object DefaultValue { get; }

        public EntryParameterDefinition(string name, bool critical, object defaultValue = null)
        {
            Name = name;
            Critical = critical;
            DefaultValue = defaultValue;
        }
    }

    /// <summary>
    /// Parses a job's JobProperties against a known set of entry parameter
    /// definitions. Missing, non-critical parameters and unrecognized extra
    /// parameters are logged and skipped rather than causing a failure; a
    /// missing critical parameter fails parsing with a clear message instead
    /// of throwing.
    /// </summary>
    public static class EntryParameterParser
    {
        public static bool TryParse(
            IDictionary<string, object> jobProperties,
            IEnumerable<EntryParameterDefinition> definitions,
            ILogger logger,
            out Dictionary<string, object> values,
            out string errorMessage)
        {
            values = new Dictionary<string, object>();
            errorMessage = null;
            jobProperties ??= new Dictionary<string, object>();
            var definitionList = definitions?.ToList() ?? new List<EntryParameterDefinition>();

            foreach (var definition in definitionList)
            {
                if (jobProperties.ContainsKey(definition.Name))
                {
                    values[definition.Name] = jobProperties[definition.Name];
                    continue;
                }

                if (definition.Critical)
                {
                    errorMessage = $"Required entry parameter '{definition.Name}' was not provided.";
                    logger?.LogError(errorMessage);
                    return false;
                }

                logger?.LogWarning($"Entry parameter '{definition.Name}' was not provided; defaulting to '{definition.DefaultValue}'.");
                values[definition.Name] = definition.DefaultValue;
            }

            var knownNames = new HashSet<string>(definitionList.Select(d => d.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var key in jobProperties.Keys)
            {
                if (!knownNames.Contains(key))
                {
                    logger?.LogWarning($"Entry parameter '{key}' is not recognized by this extension and will be ignored.");
                }
            }

            return true;
        }
    }
}
