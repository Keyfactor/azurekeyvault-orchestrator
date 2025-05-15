## Overview

The Azure Key Vault Universal Orchestrator extension enables seamless integration between Keyfactor Command and
Microsoft Azure Key Vault. This extension facilitates remote management of cryptographic certificates stored in Azure
Key Vault, ensuring organizational security and compliance requirements are met. With this extension, users can manage
certificates remotely by performing various operations such as inventory, addition, removal, and discovery of
certificates and certificate stores.

Azure Key Vault is a cloud service that provides secure storage for secrets, including cryptographic keys and
certificates. Certificates in Azure Key Vault are used to secure communications and safeguard data by managing the
associated cryptographic keys and policies.

Defined Certificate Stores of the Certificate Store Type in Keyfactor Command represent the individual or grouped
certificates managed within a specific remote location, such as Azure Key Vault. Each Certificate Store is configured to
interface with an Azure Key Vault instance, allowing the orchestrator to perform the required certificate management
operations.

## Requirements

### Setup and Configuration

The high-level steps required to configure the Azure Keyvault Orchestrator extension are:

1) [Migrating from the Windows Orchestrator for Azure KeyVault](#migrating-from-the-windows-orchestrator-for-azure-keyvault)

2) [Configure the Azure Keyvault for client access](#configure-the-azure-keyvault-for-client-access)

3) [Create the Store Type in Keyfactor](#creation-using-kfutil)

4) [Install the Extension on the Orchestrator](#installation)

5) [Create the Certificate Store](#store-creation)

_Note that the certificate store type used by this Universal Orchestrator support for Azure Keyvault is not compatible
with the certificate store type used by with Windows Orchestrator version for Azure Keyvault.
If your Keyfactor instance has used the Windows Orchestrator for Azure Keyvault, a specific migration process is
required.
See [Migrating from the Windows Orchestrator for Azure KeyVault](#migrating-from-the-windows-orchestrator-for-azure-keyvault)
section below._

#### Migrating from the Windows Orchestrator for Azure KeyVault

<details><summary>Click to expand</summary>
If you were previously using the Azure Keyvault extension for the **Windows** Orchestrator, it is necessary to remove the Store Type definition as well as any Certificate stores that use the previous store type.
This is because the store type parameters have changed to facilitate the Discovery and Create functionality.

If you have an existing AKV store type that was created for use with the Windows Orchestrator, you will need to follow
the steps in one of the below sections to transfer the capability to the Universal Orchestrator.

> :warning:
> Before removing the certificate stores, view their configuration details and copy the values.
> Copying the values in the store parameters will save time when re-creating the stores.

Follow the below steps to remove the AKV capability from **each** active Windows Orchestrator that supports it:

##### If the Windows Orchestrator should still manage other cert store types

_If the Windows Orchestrator is still used to manage some store types, we will remove only the Azure Keyvault
functionality._

1) On the Windows Orchestrator host machine, run the Keyfactor Agent Configuration Wizard
2) Proceed through the steps to "Select Features"
3) Expand "Cert Stores" and un-check "Azure Keyvault"
4) Click "Apply Configuration"

5) Open the Keyfactor Platform and navigate to **Orchestrators > Management**
6) Confirm that "AKV" no longer appears under "Capabilities"
7) Navigate to **Orchestrators > Management**, select the orchestrator and click "DISAPPROVE" to disapprove it and
   cancel pending jobs.
8) Navigate to **Locations > Certificate Stores**
9) Select any stores with the Category `Azure Keyvault` and click `DELETE` to remove them from Keyfactor.
10) Navigate to the Administrative menu (gear icon) and then **> Certificate Store Types**
11) Select `Azure Keyvault`, click `DELETE` and confirm.
12) Navigate to **Orchestrators > Management**, select the orchestrator and click "APPROVE" to re-approve it for use.

13) Repeat these steps for any other Windows Orchestrators that support the AKV store type.

##### If the Windows Orchestrator can be retired completely

_If the Windows Orchestrator is being completely replaced with the Universal Orchestrator, we can remove all associated
stores and jobs._

1) Navigate to **Orchestrators > Management** and select the Windows Orchestrator from the list.
2) With the orchestrator selected, click the "RESET" button at the top of the list
3) Make sure the orchestrator is still selected and click `DISAPPROVE`.
4) Click `OK` to confirm that you will remove all jobs and certificate stores associated with this orchestrator.
5) Navigate to the Administrative (gear icon in the top right) and then **Certificate Store Types**
6) Select `Azure Keyvault` click `DELETE` and confirm.
7) Repeat these steps for any other Windows Orchestrators that support the AKV store type (if they can also be retired).

Note: Any Azure Keyvault certificate stores removed can be re-added once the Universal Orchestrator is configured with
the AKV capability.

</details>

#### Migrating from version 1.x or version 2.x of the Azure Keyvault Orchestrator Extension

<details><summary>Click to expand</summary>
It is not necessary to re-create all the certificate stores when migrating from a previous version of this extension,
though it is important to note that Azure KeyVaults found during a Discovery job
will return with latest store path format: `{subscription id}:{resource group name}:{new vault name}`.

</details>

---

#### Configure the Azure Keyvault for client access

In order for this orchestrator extension to be able to interact with your instances of Azure Keyvault, it will need to
authenticate with an identity that has sufficient permissions to perform the jobs. Microsoft Azure implements both
Role-Based Access Control (RBAC) and the classic Access Policy method. RBAC is the preferred method, as it allows the
assignment of granular level, inheritable access control on both the contents of the KeyVaults, and higher-level
management operations. For more information and a comparison of the two access control strategies, refer
to [this article](learn.microsoft.com/en-us/azure/key-vault/general/rbac-access-policy).

##### RBAC vs. Access Policies

Azure KeyVaults originally used access policies for permissions, and since then, Microsoft has begun recommending
Role-Based Access Control (RBAC) as the preferred method of authorization.  
As of this version, new KeyVaults created via this integration are created with Access Policy authorization. This will
change to RBAC in the next release.
The access control type the KeyVault implements can be changed in the KeyVault configuration within the Azure Portal.
New KeyVaults created via Keyfactor by way of this integration will be accessible for later actions regardless of
the access control type.

##### Configure Role-Based Access Control (RBAC)

To illustrate the minimum permissions that the authenticating entity (service principal or managed identity)
requires,
we have created `3` separate custom role definitions that you can use as a reference when creating an RBAC role
definition
in your Azure environment.

The reason for `3` definitions is that certain orchestrator jobs, such as Create (new KeyVault) or Discovery require
more
elevated permissions at a different scope than the basic certificate operations (Inventory, Add, Remove) performed
within a specific KeyVault.

If you know that you will use all the capabilities of this integration; the last custom role definition contains
all necessary permissions for performing all the Jobs (Discovery, Create KeyVault, Inventory/Add/Remove
certificates).

##### Built-in vs. custom roles

> :warning: The custom role definitions below are designed to contain the absolute minimum permissions required. They
> are not intended to be used verbatim without consulting your organization's security team and/or Azure Administrator.
> Keyfactor does not provide consulting on internal security practices.

It is possible to use the built-in roles provided by Microsoft for these operations. The built-in roles may contain more
permissions than necessary.
Whether to create custom role definitions or use an existing or pre-built role will depend on your organization's
security requirements.  
For each job type performed by this orchestrator, we've included the minimally sufficient built-in role name(s) along
with our custom role definitions that limit permissions to the specific actions and scopes necessary.

#### Create Vault permissions

<details><summary>Click to expand</summary>

To allow for the ability to create new Azure KeyVaults from within command, here is a role that defines the
necessary permissions to do so. If you are never creating new Azure KeyVaults from within Command, then it is
unnecessary to provide the authenticating entity with these permissions.

> :warning: When creating a new KeyVault, we grant the creating entity the built-in "Key Vault Certificates Officer"
> role to be able to perform later actions on the contents of the
>
KeyVault. [click here](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
> to see the list of permissions included in the Key Vault Certificates Officer built-in role.

- built-in roles (both are required):
    - ["Key Vault Contributor"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)
    - ["Key Vault Access Administrator"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-data-access-administrator)

- lowest level scope required: a resource group that will contain the new KeyVault.

- condition:

```js
"((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985}))"
```

The above condition limits the ability to assign roles to a single role only (Key Vault Certificates Officer). This is
more restrictive than the condition on the built-in role
of [Key Vault Access Administrator](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-data-access-administrator).

- custom role definition:

```json
{
    "properties": {
        "roleName": "KeyfactorVaultCreator",
        "description": "This role contains all of the necessary permissions to perform Inventory, Add and Remove operations on certificates on All KeyVaults within a Resource Group.  It also contains sufficient permissions to create a new KeyVault within the resource group.",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", 
          "/subscriptions/{subscriptionId2}", 
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}",
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}",  
          "/providers/Microsoft.Management/managementGroups/{groupId1}"            
        ],
        "permissions": [
            {
                "actions": [
                    "Microsoft.KeyVault/vaults/*",
                    "Microsoft.Authorization/*/read",                                        
                    "Microsoft.KeyVault/register/action",                    
                    "Microsoft.KeyVault/checkNameAvailability/read",
                    "Microsoft.KeyVault/vaults/accessPolicies/*",
                    "Microsoft.Resources/deployments/*",
                    "Microsoft.KeyVault/locations/*/read",
                    "Microsoft.Resources/subscriptions/resourceGroups/read",
                    "Microsoft.Management/managementGroups/read",
                    "Microsoft.Resources/subscriptions/read",
                    "Microsoft.Authorization/roleAssignments/*",                     
                    "Microsoft.KeyVault/operations/read"                    
                ],
                "notActions": [],
                "dataActions": [],
                "notDataActions": [],
                "conditionVersion": "2.0",
                "condition": "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985}))"
            }
        ]
    }
}
```

</details>

#### Discover Vaults Permissions

<details><summary>Click to expand</summary>

If you would like this integration to search across your subscriptions to discover instances of existing Azure
KeyVaults, this role definition contains the necessary permissions for this.
If you are working with a smaller number of KeyVaults and/or do not plan on using a Discovery job to retrieve all
KeyVaults across your subscriptions, the permissions defined in this role are not necessary.

- built-in
  role: ["Key Vault Reader"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-reader)
- lowest level scope: a resource group
- custom role definition:

```json
{
    "properties": {
        "roleName": "KeyfactorVaultDiscovery",
        "description": "This role contains all of the necessary permissions to search for KeyVaults across a subscription",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", 
          "/subscriptions/{subscriptionId2}", 
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", 
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}",  
          "/providers/Microsoft.Management/managementGroups/{groupId1}"           
        ],
        "permissions": [
            {
              "actions": [
                "Microsoft.Authorization/*/read",
                "Microsoft.Resources/subscriptions/resourceGroups/read",
                "Microsoft.KeyVault/checkNameAvailability/read",
                "Microsoft.KeyVault/locations/*/read",
                "Microsoft.KeyVault/vaults/read",
                "Microsoft.KeyVault/operations/read"
               ],
               "notActions": [],
               "dataActions": [],
               "notDataActions": []  
             }
          ]
    }
}
```

</details>

#### Inventory, Add, and Remove Certificate Permissions

<details><summary>Click to expand</summary>

This set of permissions is the minimum required to support the basic operations of performing an Inventory and
Add/Removal of certificates.

- built-in
  role: ["Key Vault Certificates Officer"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
- lowest level scope: an individual keyvault
- custom role definition:

```json
{
    "properties": {
        "roleName": "KeyfactorManageCerts",
        "description": "This role contains all of the necessary permissions to perform Inventory, Add and Remove operations on certificates on All KeyVaults within the scope.",
        "assignableScopes": [
          "/providers/Microsoft.Management/managementGroups/{groupId1}",
          "/subscriptions/{subscriptionId}", 
          "/subscriptions/{subscriptionId2}", 
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", 
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}",                
          "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.KeyVault/vaults/{vaultName}",
          "/subscriptions/{subscriptionId2}/resourceGroups/{resourceGroupName2}/providers/Microsoft.KeyVault/vaults/{vaultName2}"
        ],
         "permissions": [
           {
             "actions": [
               "Microsoft.Authorization/*/read",
               "Microsoft.Resources/deployments/*",
               "Microsoft.Resources/subscriptions/resourceGroups/read",
               "Microsoft.KeyVault/checkNameAvailability/read",
               "Microsoft.KeyVault/locations/*/read",
               "Microsoft.KeyVault/vaults/*/read",
               "Microsoft.KeyVault/operations/read"               
             ],
             "notActions": [],
             "dataActions": [
               "Microsoft.KeyVault/vaults/certificates/*",
               "Microsoft.KeyVault/vaults/certificatecas/*",
               "Microsoft.KeyVault/vaults/keys/*",
               "Microsoft.KeyVault/vaults/secrets/readMetadata/action"
             ],
             "notDataActions": []
           }
    ]
  }
}
```

</details>

#### Combined permissions for all operations (Create, Discovery, Inventory, Add and Remove certificates)

<details><summary>Click to expand</summary>

This section defines a single custom role that contains the necessary permissions to perform all operations allowed by
this integration. The minimum scope allowable is an individual resource group. If this custom role is associated with
the authenticating identity, it will be able to discover existing KeyVaults, Create new ones, and perform inventory as
well as adding and removing certificates within the KeyVault.

- minimally sufficient built-in roles (all are required):
    - ["Key Vault Certificates Officer"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
    - ["Key Vault Contributor"](learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)
    - ["Key Vault Access Administrator"](learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/)
- lowest level scope: an individual resource group
- custom role definition:

```json
{
    "properties": {
        "roleName": "KeyfactorKeyVaultOperations",
        "description": "This role contains all of the necessary permissions to perform Discovery, Create, Inventory, Add and Remove operations on certificates on All KeyVaults within The scope.",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", 
          "/subscriptions/{subscriptionId2}", 
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}",
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}",  
          "/providers/Microsoft.Management/managementGroups/{groupId1}"            
        ],
        "permissions": [
            {
                "actions": [
                    "Microsoft.KeyVault/vaults/*",
                    "Microsoft.Authorization/*/read",                                        
                    "Microsoft.KeyVault/register/action",                    
                    "Microsoft.KeyVault/checkNameAvailability/read",
                    "Microsoft.KeyVault/vaults/accessPolicies/*",
                    "Microsoft.Resources/deployments/*",
                    "Microsoft.Resources/subscriptions/resourceGroups/read",
                    "Microsoft.Management/managementGroups/read",
                    "Microsoft.Resources/subscriptions/read",
                    "Microsoft.Authorization/roleAssignments/*",                     
                    "Microsoft.KeyVault/operations/read",                                
                    "Microsoft.KeyVault/locations/*/read",
                    "Microsoft.KeyVault/vaults/*/read"
                ],
                "notActions": [],
                "dataActions": [
                   "Microsoft.KeyVault/vaults/certificates/*",
                   "Microsoft.KeyVault/vaults/certificatecas/*",
                   "Microsoft.KeyVault/vaults/keys/*",
                   "Microsoft.KeyVault/vaults/secrets/*"
             ],
                "notDataActions": [],
                "conditionVersion": "2.0",
                "condition": "((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985}))"
            }
        ]
    }
}
```

> :warning: You still may decide to split the capabilities into separate roles to apply each of them to the
> lowest level scope
> required. We have tried to provide you with an absolute minimum set of required permissions necessary to perform each
> operation. Refer to
> your organization's security policies and/or consult with your information security team to determine which
> role combinations would
> be most appropriate for your needs.

</details>

#### Endpoint Access / Firewall

At a minimum, the orchestrator needs access to the following URLs:

- The instance of Keyfactor Command
- `login.microsoftonline.com` (or the endpoint corresponding to the Azure Global Cloud instance `Government, China,
  Germany`).
    - this is only technically necessary if they are using Service Principal authentication.
- `management.azure.com` for all management operations (Create, Add, Remove) as well as Discovery.
    - This is necessary for authenticating the ARM client used to perform these operations.

Any firewall applied to the orchestrator host will need to be configured to allow access to these endpoints in order for
this integration to make the necessary API requests.

> :warning: Discovery jobs are not supported for KeyVaults located outside the Azure Public cloud or Keyvaults
> accessed via a private url endpoint.  
> All other job types implemented by this integration are supported for alternate Azure clouds and private endpoints.

#### Authentication options

The Azure KeyVault orchestrator plugin supports several authentication options:

- [Service Principal](#authentication-via-service-principal)
- [User Assigned Managed Identities](#authentication-via-user-assigned-managed-identity)
- [System Assigned Managed Identities](#authentication-via-system-assigned-managed-identity)

Steps for setting up each option are detailed below.

#### Authentication via Service Principal

<details><summary>Click to expand</summary>

For the Orchestrator to be able to interact with the instance of Azure Keyvault, we will need to create an entity in
Azure that will encapsulate the permissions we would like to grant it. In Azure, these intermediate entities are
referred to as app registrations, and they provision authority for external application access.
To learn more about application and service principals, refer
to [this article](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal).

To provision access to the Keyvault instance using a service principal identity, we will:

1) [Create a Service Principal in Azure Active Directory](#create-a-service-principal)

2) [Assign it sufficient permissions for Keyvault operations](#assign-permissions)

3) [Generate an Access Token for Authenticating](#generate-an-access-token)

4) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

**To complete these steps, you must have the _Owner_ role for the Azure subscription, at least temporarily.**
This is required to create an App Registration in Azure Active Directory.

#### Create A Service Principal

**Note:** To manage key vaults in multiple Azure tenants using a single service principal, the supported
account types option selected should be:
`Accounts in any organizational directory (Any Azure AD directory - Multitenant)`. Also, the app registration must be
registered in a single tenant, but a service principal must be created in each tenant tied to the app registration. For
more info review
the [Microsoft documentation](https://learn.microsoft.com/en-us/azure/active-directory/fundamentals/service-accounts-principal#tenant-service-principal-relationships).

For detailed instructions on how to create a service principal in Azure, [see here](create_sp_azure.md).

Once we have our App registration created in Azure, record the following values

- _TenantId_
- _ApplicationId_
- _ClientSecret_

We will store these values securely in Keyfactor in later steps.

</details>

#### Authentication via User Assigned Managed Identity

<details><summary>Click to expand</summary>

If the orchestrator is running on an Azure Virtual
Machine, [Azure Managed identities](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview)
are supported. This allows an Azure administrator to assign a managed identity to the virtual machine to be used by this
orchestrator extension for authentication without the need to issue or manage client secrets.

The two types of managed identities available in Azure are _System_ assigned and _User_ assigned identities.

- System-assigned managed identities are bound to the specific resource and not reassignable. They are bound to the
  resource and share the same lifecycle.
- User-assigned managed identities exist as a standalone entity, independent of a resource, and can therefore be
  assigned to multiple Azure resources.

Read more about Azure Managed
Identities [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview).

Detailed steps for creating a managed identity and assigning permissions can be
found [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).

Once the User Assigned managed identity has been created, you will need only to enter the Client ID into the Application
ID field on the certificate store definition (the Client Secret can be left blank).

</details>

#### Authentication via System Assigned Managed Identity

<details>
<summary>Click to expand</summary>

To use a _System_ assigned managed identity, there is no need to enter the server credentials. If no server
credentials are provided, the extension assumes authentication is via system-assigned managed identity.

</details>


