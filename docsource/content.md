## Overview

This integration allows the orchestrator to act as a client with access to an instance of the Azure Key Vault; allowing you to manage your certificates stored in the Azure Keyvault via Keyfactor.


## Requirements

### Configure the Azure Keyvault for client access

In order for this orchestrator extension to be able to interact with your instances of Azure Keyvault, it will need to authenticate with a identity that has sufficient permissions to perform the jobs.  Microsoft Azure implements both Role Based Access Control (RBAC) and the classic Access Policy method.  RBAC is the preferred method, as it allows the assignment of granular level, inheretable access control on both the contents of the KeyVaults, as well as higher-level management operations.  For more information and a comparison of the two access control strategies, refer to [this article](learn.microsoft.com/en-us/azure/key-vault/general/rbac-access-policy).

#### RBAC vs Access Policies
Azure KeyVaults originally utilized access policies for permissions and since then, Microsoft has begun recommending Role Based Access Control (RBAC) as the preferred method of authorization.  
As of this version, new KeyVaults created via this integration are created with Access Policy authorization.  This will change to RBAC in the next release.
The access control type the KeyVault implements can be changed in the KeyVault configuration within the Azure Portal.  New KeyVaults created via Keyfactor by way of this integration will be accessible for subsequent actions regardless of the access control type.

#### Configure Role Based Access Control (RBAC)

In order to illustrate the minimum permissions that the authenticating entity (service principal or managed identity) requires, 
we have created 3 seperate custom role definitions that you can use as a reference when creating an RBAC role definition in your Azure environment.  

The reason for 3 definitions is that certain orchestrator jobs, such as Create (new KeyVault) or Discovery require more elevated permissions at a different scope than the basic certificate operations (Inventory, Add, Remove) performed within a specific KeyVault.

If you know that you will utilize all of the capabilities of this integration; the last custom role definition contains all necessary permissions for performing all of the Jobs (Discovery, Create KeyVault, Inventory/Add/Remove certificates).  

#### Built-in vs. custom roles

> :warning: The custom role definitions below are designed to contain the absolute minimum permissions required.  They are not intended to be used verbatim without consulting your organization's security team and/or Azure Administrator.  Keyfactor does not provide consulting on internal security practices.

It is possible to use the built-in roles provided by Microsoft for these operations.  The built-in roles may contain more permissions than necessary.
Whether to create custom role definitions or use an existing or pre-built role will depend on your organization's securuity requirements.  
For each job type performed by this orchestrator, we've included the minimally sufficient built-in role name(s) along with our custom role definitions that limit permissions to the specific actions and scopes necessary.

<details><summary><h4>Create Vault permissions</h4></summary>
In order to allow for the ability to create new Azure KeyVaults from within command, here is a role that defines the necessary permissions to do so.  If you will never be creating new Azure KeyVaults from within Command, then it is unnecessary to provide the authenticating entity with these permissions.

> :warning: When creating a new KeyVault, we grant the creating entity the built-in "Key Vault Certificates Officer" role in order to be able to perform subsequent actions on the contents of the KeyVault. [click here](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer) to see the list of permissions included in the Key Vault Certificates Officer built-in role.

- built-in roles (both are required):
  - ["Key Vault Contributor"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)
  - ["Key Vault Access Administrator"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-data-access-administrator)

- lowest level scope required - a resource group that will contain the new KeyVault.

- condition:

```js
"((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals{a4417e6f-fecd-4de8-b567-7b0420556985}))"
```

the above condition limits the ability to assign roles to a single role only (Key Vault Certificates Officer).  This is more restrictive than the condition on the built-in role of [Key Vault Access Administrator](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-data-access-administrator).

- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorVaultCreator",
        "description": "This role contains all of the necessary permissions to perform Inventory, Add and Remove operations on certificates on All KeyVaults within a Resource Group.  It also contains sufficient permissions to create a new KeyVault within the resource group.",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", // allow to be applied to a specific subscription
          "/subscriptions/{subscriptionId2}", // and another.. etc.
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", // allow to be scoped to a specific resource group
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}", // and another.. 
          "/providers/Microsoft.Management/managementGroups/{groupId1}" // allow to be applied for all subscriptions under management group           
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
<details><summary><h4>Discover Vaults Permissions</h4></summary>

If you would like this integration to search across your subscriptions to discover instances of existing Azure KeyVaults, this role definition contains the necessary permissions for this.
If you are working with a smaller number of KeyVaults and/or do not plan on utilizing a Discovery job to retrieve all KeyVaults across your subscriptions, the permissions defined in this role are not necessary.

- built-in role: ["Key Vault Reader"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-reader)
- lowest level scope - a resource group
- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorVaultDiscovery",
        "description": "This role contains all of the necessary permissions to search for KeyVaults across a subscription",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", // allow to be applied to a specific subscription
          "/subscriptions/{subscriptionId2}", // and another.. etc.
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", // allow to be scoped to a specific resource group
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}", // and another.. 
          "/providers/Microsoft.Management/managementGroups/{groupId1}" // allow to be applied for all resources under management group           
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
               "dataActions": [        
               ],
               "notDataActions": [],  
             }
          ]
    }
}
```

</details>
<details>
<summary><h4>Inventory, Add, and Remove Certificate Permissions</h4></summary>
This set of permissions is the minimum required to support the basic operations of performing an Inventory and Add/Removal of certificates.

- built-in role: ["Key Vault Certificates Officer"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
- lowest level scope - an individual keyvault
- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorManageCerts",
        "description": "This role contains all of the necessary permissions to perform Inventory, Add and Remove operations on certificates on All KeyVaults within the scope.",
        "assignableScopes": [
          "/providers/Microsoft.Management/managementGroups/{groupId1}", // allow scope for all subscriptions under management group
          "/subscriptions/{subscriptionId}", // allow to scoped to a specific subscription
          "/subscriptions/{subscriptionId2}", // and another.. etc.
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", // allow to be scoped to a specific resource group
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}", // and another..               
          "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.KeyVault/vaults/{vaultName}", // allow scope to a specific vault
          "/subscriptions/{subscriptionId2}/resourceGroups/{resourceGroupName2}/providers/Microsoft.KeyVault/vaults/{vaultName2}", // .. and another
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
               "Microsoft.KeyVault/operations/read",               
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
    ],
  }
}
```

</details>
<details>
<summary><h4>Combined permissions for all operations (Create, Discovery, Inventory, Add and Remove certificates)</h4></summary>
This section defines a single custom role that contains the necessary permissions to perform all operations allowed by this integration.  The minimum scope allowable is an individual resource group.  If this custom role is associated with the authenticating identity, it will be able to discover existing KeyVaults, Create new ones, and perform inventory as well as adding and removing certificates within the KeyVault.

- minimally sufficient built-in roles (all are required):
  - ["Key Vault Certificates Officer"](github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
  - ["Key Vault Contributor"](learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)
  - ["Key Vault Access Administrator"](learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/)
- lowest level scope - an individual resource group
- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorKeyVaultOperations",
        "description": "This role contains all of the necessary permissions to perform Discovery, Create, Inventory, Add and Remove operations on certificates on All KeyVaults within The scope.",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", // allow to be applied to a specific subscription
          "/subscriptions/{subscriptionId2}", // and another.. etc.
          "/subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}", // allow to be scoped to a specific resource group
          "/subscriptions/{subscriptionId2}/resourcegroups/{resourceGroupName2}", // and another.. 
          "/providers/Microsoft.Management/managementGroups/{groupId1}" // allow to be applied for all subscriptions under management group           
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
                    "Microsoft.KeyVault/operations/read"                                
                    "Microsoft.KeyVault/locations/*/read",
                    "Microsoft.KeyVault/vaults/*/read",
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

> :warning: You still may decide to split the capabilities into seperate roles in order to apply each of them to the lowest level scope
> required.  We have tried to provide you with an absolute minimum set of required permissions necessary to perform each operation.  Refer to
> your organization's security policies and/or consult with your information security team in order to determine which role combinations would
> be most appropriate for your needs.
</details>

### Endpoint Access / Firewall

At a minimum, the orchestrator needs access to the following URLs:

- The instance of Keyfactor Command
- 'login.microsoftonline.com' (or the endpoint corresponding to the Azure Global Cloud instance (Government, China, Germany).
  - this is only technically necessary if they are using Service Principal authentication.
- 'management.azure.com' for all management operations (Create, Add, Remove) as well as Discovery.
  - This is necessary for authenticating the ARM client used to perform these operations.

Any firewall applied to the orchestrator host will need to be configured to allow access to these endpoints in order for this integration to make the necessary API requests.

> :warning: Discovery jobs are not supported for KeyVaults located outside of the Azure Public cloud or Keyvaults accessed via a private url endpoint.  
> All other job types implemented by this integration are supported for alternate Azure clouds and private endpoints.

### Authentication options

The Azure KeyVault orchestrator plugin supports several authentication options:

- [Service Principal](#authentication-via-service-principal)
- [User Assigned Managed Identities](#authentication-via-user-assigned-managed-identity)
- [System Assigned Managed Identities](#authentication-via-system-assigned-managed-identity)

 Steps for setting up each option are detailed below.

<details>
<summary><h4>Authentication via Service Principal</h4></summary>

For the Orchestrator to be able to interact with the instance of Azure Keyvault, we will need to create an entity in Azure that will encapsulate the permissions we would like to grant it.  In Azure, these intermediate entities are referred to as app registrations and they provision authority for external application access.
To learn more about application and service principals, refer to [this article](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal).

To provision access to the Keyvault instance using a service principal identity, we will:

1) [Create a Service Principal in Azure Active Directory](#create-a-service-principal)

1) [Assign it sufficient permissions for Keyvault operations](#assign-permissions)

1) [Generate an Access Token for Authenticating](#generate-an-access-token)

1) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

**In order to complete these steps, you must have the _Owner_ role for the Azure subscription, at least temporarily.**
This is required to create an App Registration in Azure Active Directory.

### Create A Service Principal

**Note:** In order to manage key vaults in multiple Azure tenants using a single service principal, the supported account types option selected should be:  `Accounts in any organizational directory (Any Azure AD directory - Multitenant)`. Also, the app registration must be registered in a single tenant, but a service principal must be created in each tenant tied to the app registration. For more info review the [Microsoft documentation](https://learn.microsoft.com/en-us/azure/active-directory/fundamentals/service-accounts-principal#tenant-service-principal-relationships).

For detailed instructions on how to create a service principal in Azure, [see here](create_sp_azure.md).

Once we have our App registration created in Azure, record the following values

- _TenantId_
- _ApplicationId_
- _ClientSecret_

We will store these values securely in Keyfactor in subsequent steps.
</details>
<details>
<summary><h4>Authentication via User Assigned Managed Identity</h4></summary>
Authentication has been somewhat simplified with the introduction of Azure Managed Identities.  If the orchestrator is running on an Azure Virtual Machine, Managed identities allow an Azure administrator to
assign a managed identity to the virtual machine that can then be used by this orchestrator extension for authentication without the need to issue or manage client secrets.

The two types of managed identities available in Azure are _System_ assigned, and _User_ assigned identities.

- System assigned managed identities are bound to the specific resource and not reassignable.  They are bound to the resource and share the same lifecycle.  
- User assigned managed identities exist as a standalone entity, independent of a resource, and can therefore be assigned to multiple Azure resources.

Read more about Azure Managed Identities [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview).

Detailed steps for creating a managed identity and assigning permissions can be found [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).

Once the User Assigned managed identity has been created, you will need only to enter the Client Id into the Application Id field on the certificate store definition (the Client Secret can be left blank).
</details>
</details>
<details>
<summary><h4>Authentication via System Assigned Managed Identity</h4></summary>
In order to use a _System_ assigned managed identity, there is no need to enter the server credentials.  If no server credentials are provided, the extension assumes authentication is via system assigned managed identity.
</details>


## Discovery

Now that we have the extension registered on the Orchestrator, we can navigate back to the Keyfactor platform and finish the setup.  If there are existing Azure Key Vaults, complete the below steps to discover and add them.  If there are no existing key vaults to integrate and you will be creating a new one via the Keyfactor Platform, you can skip to the next section.

1) Navigate to Orchestrators > Management in the platform.

     ![Manage Orchestrators](/Images/orch-manage.png)

1) Find the row corresponding to the orchestrator that we just installed the extension on.

1) If the store type has been created and the integration installed on the orchestrator, you should see the _AKV_ capability in the list.

     ![AKV Capability](/Images/akv-capability.png)

1) Approve the orchestrator if necessary.

#### Create the discovery job

1) Navigate to "Locations > Certificate Stores"

     ![Locations Cert Stores](/Images/locations-certstores.png)

1) Click the "Discover" tab, and then the "Schedule" button.

     ![Discovery Schedule](/Images/discover-schedule.png)

1) You should see the form for creating the Discovery job.

     ![Discovery Form](/Images/discovery-form.png)

#### Store the Server Credentials in Keyfactor

> :warning:
> The steps for configuring discovery are different for each authentication type.

- For System Assigned managed identity authentication this step can be skipped.  No server credentials are necessary.  The store type should have been set up without "needs server" checked, so the form field should not be present.  

- For User assigned managed identity:
  - `Client Machine` should be set to the GUID of the tenant ID of the instance of Azure Keyvault.
  - `User` should be set to the Client ID of the managed identity.
  - `Password` should be set to the value **"managed"**.

- For Service principal authentication:
  - `Client Machine` should be set to the GUID of the tenant ID of the instance of Azure Keyvault. **Note:** If using a multi-tenant app registration, use the tenant ID of the Azure tenant where the key vault lives.
  - `User` should be set to the service principal id
  - `Password` should be set to the client secret.

The first thing we'll need to do is store the server credentials that will be used by the extension.
The combination of fields required to interact with the Azure Keyvault are:

- Tenant (or Directory) ID
- Application ID or user managed identity ID
- Client Secret (if using Service Principal Authentication)

If not using system managed identity authentication, the integration expects the above values to be included in the server credentials in the following way:

- **Client Machine**: `<tenantId>` (GUID)

- **User**: `<app id guid>` (if service principal authentication) `<managed user id>` (if user managed identity authentication is used)

- **Password**: `<client secret>` (if service principal authentication), `managed` (if user managed identity authentication is used)

Follow these steps to store the values:

1) Enter the _Tenant Id_ in the **Client Machine** field.

     ![Discovery Form](/Images/discovery-form-client-machine.png)

1) Click "Change Credentials" to open up the Server Credentials form.

     ![Change Credentials](/Images/change-credentials-form.png)

1) Click "UPDATE SERVER USERNAME" and Enter the appropriate values based on the authentication type.

      ![Set Username](/Images/server-creds-username.png)

1) Enter again to confirm, and click save.

1) Click "UPDATE SERVER PASSWORD" and update with the appropriate value (`<client secret>` or `managed`) following the same steps as above.

1) Select a time to run the discovery job.

1) Enter commma seperated list of tenant ID's in the "Directories to search" field.'

> :warning:
> If nothing is entered here, the default Tenant ID included with the credentials will be used.  For system managed identities, it is necessary to include the Tenant ID(s) in this field.

1) Leave the remaining fields blank and click "SAVE".

#### Approve the Certificate Store

When the Discovery job runs successfully, it will list the existing Azure Keyvaults that are acessible by our service principal.

In this example, our job returned these Azure Keyvaults.

![Discovery Results](/Images/discovery-result.png)

The store path of each vault is the `<subscription id>:<resource group name>:<vault name>`:

![Discovery Results](/Images/storepath.png)

To add one of these results to Keyfactor as a certificate store:

1) Double-click the row that corresponds to the Azure Keyvault in the discovery results (you can also select the row and click "SAVE").

1) In the dialog window, enter values for any of the optional fields you have set up for your store type.

1) Select a container to store the certificates for this cert store (optional)

1) Select any value for SKU Type and Vault Region.  These values are not used for existing KeyVaults.

1) Click "SAVE".
