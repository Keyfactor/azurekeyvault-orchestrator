
### Configure Role Based Access Control (RBAC)

In order to illustrate the minimum permissions that the authenticating entity (service principal or managed identity)
requires we have created 3 separate custom role definitions that you can use as a reference when creating an RBAC role definition
in your Azure environment.

The reason for 3 definitions is that certain orchestrator jobs, such as Create (new KeyVault) or Discovery require more
elevated permissions at a different scope than the basic certificate operations (Inventory, Add, Remove) performed
within a specific KeyVault.

If you know that you will utilize all of the capabilities of this integration; the last custom role definition contains
all necessary permissions for performing all of the Jobs (Discovery, Create KeyVault, Inventory/Add/Remove
certificates).

#### Built-in vs. custom roles

> :warning: The custom role definitions below are designed to contain the absolute minimum permissions required. They
> are not intended to be used verbatim without consulting your organization's security team and/or Azure Administrator.
> Keyfactor does not provide consulting on internal security practices.

It is possible to use the built-in roles provided by Microsoft for these operations. The built-in roles may contain more
permissions than necessary.
Whether to create custom role definitions or use an existing or pre-built role will depend on your organization's
security requirements.  
For each job type performed by this orchestrator, we've included the minimally sufficient built-in role name(s) along
with our custom role definitions that limit permissions to the specific actions and scopes necessary.

The custom role definitions are derived from the Azure SDK calls this extension actually makes, and each entry is
annotated with the job step that needs it.

The scripts in [scripts/rbac](scripts/rbac/README.md) will create these role definitions, a service principal and the
role assignments for you, and verify the result by replaying the calls the extension makes - useful if you would rather
confirm the lists below than take them on trust.

<details>
  <summary><b>Create Vault permissions</b></summary>

In order to allow for the ability to create new Azure KeyVaults from within command, here is a role that defines the
necessary permissions to do so. If you will never be creating new Azure KeyVaults from within Command, then it is
unnecessary to provide the authenticating entity with these permissions.

- built-in role: ["Key Vault Contributor"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)

- lowest level scope required - a resource group that will contain the new KeyVault.

> :warning: Creating a KeyVault does not grant the authenticating entity any access to the certificates inside it, and
> this extension does not create role assignments or access policies on the vaults it creates. Assign the certificate
> permissions below at **resource group** scope so that newly created vaults inherit them, otherwise an Inventory or Add
> job against a freshly created vault will fail with a permissions error.

- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorVaultCreator",
        "description": "This role contains the necessary permissions to create a new KeyVault within the scope.  It does not grant access to the certificates within any KeyVault.",
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
                    "Microsoft.KeyVault/vaults/write", // create the vault
                    "Microsoft.KeyVault/vaults/read", // read the vault back after creating it
                    "Microsoft.KeyVault/locations/operationResults/read", // poll the result of the create operation
                    "Microsoft.Resources/subscriptions/resourceGroups/read", // resolve the target resource group
                    "Microsoft.Resources/subscriptions/providers/read", // only used when no Vault Region is supplied, to select a default location
                    "Microsoft.KeyVault/register/action" // only needed if Microsoft.KeyVault is not already registered on the subscription
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

<details>
  <summary><b>Discover Vaults Permissions</b></summary>

If you would like this integration to search across your subscriptions to discover instances of existing Azure
KeyVaults, this role definition contains the necessary permissions for this.
If you are working with a smaller number of KeyVaults and/or do not plan on utilizing a Discovery job to retrieve all
KeyVaults across your subscriptions, the permissions defined in this role are not necessary.

- built-in
  role: ["Key Vault Reader"](https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-reader)
- lowest level scope - a subscription. The Discovery job enumerates the subscriptions available to the identity and then
  lists the vaults in each one, so a resource group scoped assignment will not return results.
- custom role definition:

```js
{
    "properties": {
        "roleName": "KeyfactorVaultDiscovery",
        "description": "This role contains all of the necessary permissions to search for KeyVaults across a subscription",
        "assignableScopes": [
          "/subscriptions/{subscriptionId1}", // allow to be applied to a specific subscription
          "/subscriptions/{subscriptionId2}", // and another.. etc.
          "/providers/Microsoft.Management/managementGroups/{groupId1}" // allow to be applied for all resources under management group           
        ],
        "permissions": [
            {
              "actions": [
                "Microsoft.Resources/subscriptions/read", // list the subscriptions to be searched
                "Microsoft.KeyVault/vaults/read" // list the vaults within each subscription
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
  <summary><b>Inventory, Add, and Remove Certificate Permissions</b></summary>

This set of permissions is the minimum required to support the basic operations of performing an Inventory and
Add/Removal of certificates.

These are all data plane operations. The extension addresses the vault directly at
`https://{vaultName}.{vaultEndpoint}/` rather than looking it up through Azure Resource Manager, so no control plane
(`actions`) permissions are required for these jobs at all.

- built-in
  role: ["Key Vault Certificates Officer"](https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
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
             "actions": [], // no control plane access is used by these jobs
             "notActions": [],
             "dataActions": [
               "Microsoft.KeyVault/vaults/certificates/read", // Inventory, and the existence check performed before an Add
               "Microsoft.KeyVault/vaults/certificates/import/action", // Add
               "Microsoft.KeyVault/vaults/certificates/delete", // Remove
               "Microsoft.KeyVault/vaults/certificates/recover/action" // Add, when the alias exists in the soft-deleted state
             ],
             "notDataActions": []
           }
    ],
  }
}
```

> :memo: Only the public certificate is read back during Inventory, so no secret or key permissions are required. If you
> later need Command to retrieve the private key from the vault, that is a separate `secrets/getSecret/action` grant.

</details>

<details>
  <summary><b>Combined permissions for all operations (Create, Discovery, Inventory, Add and Remove certificates)</b></summary>

This section defines a single custom role that contains the necessary permissions to perform all operations allowed by
this integration. The minimum scope allowable is an individual resource group. If this custom role is associated with
the authenticating identity, it will be able to discover existing KeyVaults, Create new ones, and perform inventory as
well as adding and removing certificates within the KeyVault.

- minimally sufficient built-in roles (both are required):
    - ["Key Vault Certificates Officer"](https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/role-based-access-control/built-in-roles/security.md#key-vault-certificates-officer)
    - ["Key Vault Contributor"](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-contributor)
- lowest level scope - a subscription, if the Discovery job is used; otherwise an individual resource group.
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
                    "Microsoft.KeyVault/vaults/write", // Create
                    "Microsoft.KeyVault/vaults/read", // Create, Discovery
                    "Microsoft.KeyVault/locations/operationResults/read", // Create - poll the result of the create operation
                    "Microsoft.Resources/subscriptions/read", // Discovery - list the subscriptions to be searched
                    "Microsoft.Resources/subscriptions/resourceGroups/read", // Create - resolve the target resource group
                    "Microsoft.Resources/subscriptions/providers/read", // Create - only used when no Vault Region is supplied
                    "Microsoft.KeyVault/register/action" // only needed if Microsoft.KeyVault is not already registered on the subscription
                ],
                "notActions": [],
                "dataActions": [
                   "Microsoft.KeyVault/vaults/certificates/read", // Inventory, and the existence check performed before an Add
                   "Microsoft.KeyVault/vaults/certificates/import/action", // Add
                   "Microsoft.KeyVault/vaults/certificates/delete", // Remove
                   "Microsoft.KeyVault/vaults/certificates/recover/action" // Add, when the alias exists in the soft-deleted state
             ],
                "notDataActions": []
            }
        ]
    }
}
```
</details>

> :warning: You still may decide to split the capabilities into separate roles in order to apply each of them to the
> lowest level scope
> required. We have tried to provide you with an absolute minimum set of required permissions necessary to perform each
> operation. Refer to
> your organization's security policies and/or consult with your information security team in order to determine which
> role combinations would
> be most appropriate for your needs.
