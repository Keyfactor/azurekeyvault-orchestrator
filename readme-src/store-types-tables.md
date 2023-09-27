
### Azure Keyvault Store Type
#### kfutil Create Azure Keyvault Store Type
The following commands can be used with [kfutil](https://github.com/Keyfactor/kfutil). Please refer to the kfutil documentation for more information on how to use the tool to interact w/ Keyfactor Command.

```
bash
kfutil login
kfutil store - types create--name Azure Keyvault 
```

#### UI Configuration
##### UI Basic Tab
| Field Name              | Required | Value                                     |
|-------------------------|----------|-------------------------------------------|
| Name                    | &check;  | Azure Keyvault                          |
| ShortName               | &check;  | AKV                          |
| Custom Capability       |          | Unchecked [ ]                             |
| Supported Job Types     | &check;  | Inventory,Add,Create,Discovery,Remove     |
| Needs Server            | &check;  | Checked [x]                         |
| Blueprint Allowed       |          | Unchecked [ ]                       |
| Uses PowerShell         |          | Unchecked [ ]                             |
| Requires Store Password |          | Unchecked [ ]                          |
| Supports Entry Password |          | Unchecked [ ]                         |
      
![akv_basic.png](docs%2Fscreenshots%2Fstore_types%2Fakv_basic.png)

##### UI Advanced Tab
| Field Name            | Required | Value                 |
|-----------------------|----------|-----------------------|
| Store Path Type       |          | Freeform      |
| Supports Custom Alias |          | Optional |
| Private Key Handling  |          | Optional  |
| PFX Password Style    |          | Default   |

![akv_advanced.png](docs%2Fscreenshots%2Fstore_types%2Fakv_advanced.png)

##### UI Custom Fields Tab
| Name           | Display Name         | Type   | Required | Default Value |
| -------------- | -------------------- | ------ | -------- | ------------- |
|VaultName|VaultName|String|null|true|
|ResourceGroupName|ResourceGroupName|String|null|true|
|SkuType|SKU Type|MultipleChoice|standard,premium|false|
|VaultRegion|Vault Region|MultipleChoice|eastus,eastus2,southcentralus,westus2,westus3,australiaeast,northeurope,swedencentral,uksouth,westeurope,centralus,southafricanorth,centralindia,eastasia,japaneast,koreacentral,canadacentral,francecentral,germanywestcentral,norwayeast,switzerlandnorth,uaenorth,brazilsouth,centraluseuap,eastus2euap,qatarcentral,centralusstage,eastusstage,eastus2stage,northcentralusstage,westusstage,asia,asiapacific,australia,brazil,canada,europe,france,germany,global,india,japan,korea,norway,singapore,southafrica,switzerland,uae,uk,unitedstates,unitedstatesuap,eastasiastage,southeastasiastage,brazilus,eastusstg,northcentralus,westus,jioindiawest,devfabric,westcentralus,southafricawest,australiacentral,australiacentral2,australiasoutheast,japanwest,jioindiacentral,koreasouth,southindia,westindia,canadaeast,francesouth,germanynorth,norwaywest,switzerlandwest,ukwest,uaecentral,brazilsoutheast|false|
|AzureCloud|Azure Cloud|MultipleChoice|public,china,germany,government|false|
|PrivateEndpoint|Private KeyVault Endpoint|String|null|false|


**Entry Parameters:**

Entry parameters are inventoried and maintained for each entry within a certificate store.
They are typically used to support binding of a certificate to a resource.

|Name|Display Name| Type|Default Value|Required When |
|----|------------|-----|-------------|--------------|

