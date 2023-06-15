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

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    static class AzureKeyVaultConstants
    {
        public const string STORE_TYPE_NAME = "AKV";
    }

    static class JobTypes
    {
        public const string CREATE = "Create";
        public const string DISCOVERY = "Discovery";
        public const string INVENTORY = "Inventory";
        public const string MANAGEMENT = "Management";
        public const string REENROLLMENT = "Enrollment";
    }
}

