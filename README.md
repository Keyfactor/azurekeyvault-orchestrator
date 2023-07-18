# Azure Key Vault Orchestrator

This integration allows the orchestrator to act as a client with access to an instance of the Azure Key Vault; allowing you to manage your certificates stored in the Azure Keyvault via Keyfactor.

#### Integration status: Production - Ready for use in production environments.


## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.




## Support for Azure Key Vault Orchestrator

Azure Key Vault Orchestrator is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative.

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.



---




## Keyfactor Version Supported

The minimum version of the Keyfactor Universal Orchestrator Framework needed to run this version of the extension is 10.1

## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|&check; |&check; |
|Supports Discovery|&check; |&check; |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |&check; |





---


## Setup and Configuration

The high level steps required to configure the Azure Keyvault Orchestrator extension are:

1) [Migrating from the Windows Orchestrator for Azure KeyVault](#migrating-from-the-windows-orchestrator-for-azure-keyvault)

1) [Configure the Azure Keyvault for client access](#configure-the-azure-keyvault-for-client-access)

1) [Create the Store Type in Keyfactor](#create-the-store-type-in-keyfactor)

1) [Install the Extension on the Orchestrator](#install-the-extension-on-the-orchestrator)

1) [Create the Certificate Store](#create-the-certificate-store)

_Note that the certificate store type used by this Universal Orchestrator support for Azure Keyvault is not compatible with the certificate store type used by with Windows Orchestrator version for Azure Keyvault.
If your Keyfactor instance has used the Windows Orchestrator for Azure Keyvault, a specific migration process is required.
See [Migrating from the Windows Orchestrator for Azure KeyVault](#migrating-from-the-windows-orchestrator-for-azure-keyvault) section below._

---

### Migrating from the Windows Orchestrator for Azure KeyVault

If you were previously using the Azure Keyvault extension for the **Windows** Orchestrator, it is necessary to remove the Store Type definition as well as any Certificate stores that use the previous store type.
This is because the store type parameters have changed in order to facilitate the Discovery and Create functionality.

If you have an existing AKV store type that was created for use with the Windows Orchestrator, you will need to follow the steps in one of the below sections in order to transfer the capability to the Universal Orchestrator.

> :warning:
> Before removing the certificate stores, view their configuration details and copy the values.
> Copying the values in the store parameters will save time when re-creating the stores.

Follow the below steps to remove the AKV capability from **each** active Windows Orchestrator that supports it:

#### If the Windows Orchestrator should still manage other cert store types

_If the Windows Orchestrator will still be used to manage some store types, we will remove only the Azure Keyvault functionality._

1) On the Windows Orchestrator host machine, run the Keyfactor Agent Configuration Wizard
1) Proceed through the steps to "Select Features"
1) Expand "Cert Stores" and un-check "Azure Keyvault"
1) Click "Apply Configuration"

1) Open the Keyfactor Platform and navigate to **Orchestrators > Management**
1) Confirm that "AKV" no longer appears under "Capabilities"
1) Navigate to **Orchestrators > Management**, select the orchestrator and click "DISAPPROVE" to disapprove it and cancel pending jobs.
1) Navigate to **Locations > Certificate Stores**
1) Select any stores with the Category "Azure Keyvault" and click "DELETE" to remove them from Keyfactor.
1) Navigate to the Administrative menu (gear icon) and then **> Certificate Store Types**
1) Select Azure Keyvault, click "DELETE" and confirm.
1) Navigate to **Orchestrators > Management**, select the orchestrator and click "APPROVE" to re-approve it for use.

1) Repeat these steps for any other Windows Orchestrators that support the AKV store type.

#### If the Windows Orchestrator can be retired completely

_If the Windows Orchestrator is being completely replaced with the Universal Orchestrator, we can remove all associated stores and jobs._

1) Navigate to **Orchestrators > Management** and select the Windows Orchestrator from the list.
1) With the orchestrator selected, click the "RESET" button at the top of the list
1) Make sure the orchestrator is still selected, and click "DISAPPROVE".
1) Click "OK" to confirm that you will remove all jobs and certificate stores associated to this orchestrator.
1) Navigate to the the Administrative (gear icon in the top right) and then **Certificate Store Types**
1) Select "Azure Keyvault", click "DELETE" and confirm.
1) Repeat these steps for any other Windows Orchestrators that support the AKV store type (if they can also be retired).

Note: Any Azure Keyvault certificate stores removed can be re-added once the Universal Orchestrator is configured with the AKV capability.

---

### Configure the Azure Keyvault for client access

### Authentication options

The Azure KeyVault orchestrator plugin supports several authentication options:

- [Service Principal](#authentication-via-service-principal)
- [User Assigned Managed Identities](#authentication-via-user-assigned-managed-identity)
- [System Assigned Managed Identities](#authentication-via-system-assigned-managed-identity)

 Steps for setting up each option are detailed below.

#### Authentication via Service Principal

For the Orchestrator to be able to interact with the instance of Azure Keyvault, we will need to create an entity in Azure that will encapsulate the permissions we would like to grant it.  In Azure, these intermediate entities are referred to as app registrations and they provision authority for external application access.
To learn more about application and service principals, refer to [this article](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal).

To provision access to the Keyvault instance using a service principal identity, we will:

1) [Create a Service Principal in Azure Active Directory](#create-a-service-principal)

1) [Assign it sufficient permissions for Keyvault operations](#assign-permissions)

1) [Generate an Access Token for Authenticating](#generate-an-access-token)

1) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

**To create the app registration, you need at least the Application Administrator or Global Administrator Azure AD Roles. To assign the service principal to the appropriate Azure subscription, resource groups, or resources, you need at least the User Access Administrator or Owner roles.**

### Create A Service Principal

**Note:** In order to manage key vaults in multiple Azure tenants using a single service principal, the supported account types option selected should be:  `Accounts in any organizational directory (Any Azure AD directory - Multitenant)`. Also, the app registration must be registered in a single tenant, but a service principal must be created in each tenant tied to the app registration. For more info review the [Microsoft documentation](https://learn.microsoft.com/en-us/azure/active-directory/fundamentals/service-accounts-principal#tenant-service-principal-relationships).

1) Log into [your azure portal](https://portal.azure.com)

1) Navigate to [Azure active directory](https://portal.azure.com/#blade/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/Overview) in the portal.

1) Select "App registrations" from the menu.

1) Click "+ New registration"

1) Give it a name such as "keyfactor-akv" and leave the first radio button selected

     ![App Registration Overview](/Images/app-registration.PNG)

1) Once the entity has been created, you should be directed to the overview view.

     ![App Registration Overview](/Images/managed-app-link.png)

1) From here, copy the _Directory (tenant) ID_.

1) Click on the underlined link above.  You should see the managed application details that look similar to the below screen shot.

     ![App registration object Id](/Images/objectId.png)

1) Copy the _Application (client) ID_

1) Now we have a App registration and values for  _Directory (tenant) ID_, _Application (client) ID_.  These will be used by the integration for authentication to Azure.
1) (Optional) If creating a multi-tenant service principal, the following AzureAD Powershell command must be run in each tenant:  
   ``` Powershell
   New-AzADServicePrincipal -ApplicationId <Application ID>
   ```

#### Assign Permissions

In order to be able to discover and create new Azure Keyvault certificate stores, the app principal that we created must be provided with the "Keyvault Administrator" role at the _Resource Group_ level.[^1]
_If there are multiple resource groups that will contain Key Vaults to be managed, you should repeat for each._

Here are the steps for assigning this role.

1) Navigate to the Azure portal and select a resource group that will contain the Keyvaults we would like to manage.
1) Select "Access control (IAM)" from the left menu.
1) Click "Add", then "Add Role Assignment" to create a new role assignment

     ![Resource Group Add Role](/Images/resource-group-add-role.PNG)
1) Search and Select the "Key Vault Administrator" role.
1) Search and Select the principal we created.

     ![Select Principal](/Images/rg-role-select-principal.PNG)
1) Click "Review and Assign" and save the role assignment.

[^1]: If discovery and create store functionality are not neeeded, it is also possible to manage individual certificate stores without the need to provide resource group level authority.  The steps to do assign permissions for an individual Azure Keyvault are described [here](#assign-permissions-for-an-individual-key-vault-via-access-policy) for vaults using Access Policy based permissions and [here](#assign-permissions-for-an-individual-key-vault-via-rbac) for Individual Key Vaults using Role-Based Access Control (RBAC).

#### Assign Permissions for an Individual Key Vault via RBAC

If you only need to manage a single instance of a Key Vault and do not require creation and discovery of new Key Vaults, you can provision access to the specific instance without needing to provide the service principal the "Keyvault Administrator" role at the resource group level.

Follow the below steps in order to provide management access for our service principal to a specific instance of a Key Vault:

1) Navigate to the Azure Portal and then to your instance of the Azure Keyvault

1) Go to "Access control (IAM)" in the navigation menu for the Key vault.

1) Click on "Add role assignment"

     ![Vault RBAC](/Images/vault-rbac.png)

1) Find the Keyvault Administrator role in the list.  Select it and click "Next"

    ![Vault RBAC KVAdmin](/Images/vault-rbac-kvadmin.png)

1) On the next screen, click "Select members" and then search for the service principal we created above.

    ![Vault RBAC principal](/Images/vault-rbac-principal.png)

1) Select the service principal, click "select", and then "Next"

1) On the final screen, you should see something similar to the following:

     ![Vault RBAC final](/Images/vault-rbac-final.png)

1) Click "Review + assign" to finish assigning the role of Keyvault Administrator for this Key Vault to our service principal account.

#### Assign Permissions for an Individual Key Vault via Access Policy

Access to an Azure Key Vault instance can be granted via Role Based Access Control (RBAC) or with class Azure Resource Access Policies.  The below steps are for provisioning access to a single instance of a Key Vault using Access Policies.  If you are using RBAC at the resource group level (necessary for discovery and creating new Key Vaults via Keyfactor) we recommend following RBAC (above).  Alternatively, you will need to assign explicit permissions to the service principal for any Key Vault that is using Access Policy for Access Control if the Key Vault should be managed with Keyfactor.

Following the below steps will provide our service principal with the ability to manage keys in an existing vault, without providing it the elevated permissions required for discovering existing vaults or creating new ones.  If you've completed the steps in the previous section for the resource group that contains the Key Vault(s) you would like to manage and the Key Vault(s) are using RBAC, the below steps are not necessary.

1) Navigate to the Azure Portal and then to your instance of the Azure Keyvault.

1) Go to "Access Policies" in the navigation menu for the Key vault.

1) Click "+ Add Access Policy"

1) In the first drop-down, you can select "Certificate Management".  This will select all certificate management permissions.

     ![Permission List](/Images/cert-mgmt-perm-list.PNG)

1) Click "Select Principal" to open the search pane.

1) Find the Application Registration we created above, select it, and click "Select".

     ![Select Principal](/Images/select-principal.PNG)

1) Leave "Authorized application" unselected.

1) Click "Add".

1) After you are redirected to the "Access policies" view, you should see the App Registration listed under "APPLICATION".

1) Click "Save" at the top of this view.

     ![Select Principal](/Images/save-access-policy.PNG)

#### Generate an Access Token

For authenticating to Azure via App Registration/Service Principal, we will need to generate an access token.

1) Navigate to the App Registration we created earlier, in Azure Active Directory.
1) Select "Certificates & Secrets" from the left menu.
1) Click "+ New client secret"
1) Give it a description such as "Keyfactor access"
1) Select a valid expiration
1) Click "Add".
1) Copy the "Value" of the secret before navigating away.

Now we have our App registration created in Azure, and we have the following values

- _TenantId_
- _ApplicationId_
- _ClientSecret_

We will store these values securely in Keyfactor in subsequent steps.

#### Authentication via User Assigned Managed Identity

Authentication has been somewhat simplified with the introduction of Azure Managed Identities.  If the orchestrator is running on an Azure Virtual Machine, Managed identities allow an Azure administrator to
assign a managed identity to the virtual machine that can then be used by this orchestrator extension for authentication without the need to issue or manage client secrets.

The two types of managed identities available in Azure are _System_ assigned, and _User_ assigned identities.

- System assigned managed identities are bound to the specific resource and not reassignable.  They are bound to the resource and share the same lifecycle.  
- User assigned managed identities exist as a standalone entity, independent of a resource, and can therefore be assigned to multiple Azure resources.

Read more about Azure Managed Identities [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview).

Detailed steps for creating a managed identity and assigning permissions can be found [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).

Once the User Assigned managed identity has been created, you will need only to enter the Client Id into the Application Id field on the certificate store definition (the Client Secret can be left blank).

#### Authentication via System Assigned Managed Identity

In order to use a _System_ assigned managed identity, there is no need to enter the

### Create the Store Type in Keyfactor

Now we can navigate to the Keyfactor platform and create the store type for Azure Key Vault.

1) Navigate to your instance of Keyfactor and log in with a user that has Administrator privileges.

1) Click on the gear icon in the top left and navigate to "Certificate Store Types".

     ![Cert Store Types Menu](/Images/cert-store-types-menu.png)

1) Click "Add" to open the Add Certificate Store dialog.

1) Name the new store type "Azure Keyvault" and give it the short name of "AKV".

1) The Azure Keyvault integration supports the following job types: _Inventory, Add, Remove, Create and Discovery_.  Select from these the capabilities you would like to utilize.

1) **If you are using a Service Principal or User assigned Managed Identity only** Make sure that "Needs Server" is checked.

     ![Cert Store Types Menu](/Images/cert-store-type.png)

> :warning:
> if you are using a system assigned managed identity for authentication, you should leave this unchecked.

1) Navigate to the _Advanced_ tab and set the following values:
     - Store Path Type: **Freeform**
     - Supports Custom Alias: **Optional**
     - Private Key Handling: **Optional**
     - PFX Password Style: **Default**

    ![Cert Store Types Menu](/Images/store-type-fields-advanced.png)

1) Navigate to the _Custom Fields_ tab and add the following fields

     | Name | Display Name | Type | Required |
     | ---- | ------------ | ---- | -------- |
     | VaultName | Vault Name | String | true |
     | ResourceGroupName | Resource Group Name | String | true |
     | SkuType[^sku] | SKU Type | MultipleChoice | false |
     | VaultRegion[^vaultregion] | Vault Region | MultipleChoice | false |
     | TenantId | Tenant Id | String | True

     [^sku]: The SkuType determines the service tier when creating a new instance of Azure KeyVault via the platform.  Valid values include "premium" and "standard".
        If either option should be available when creating a new KeyVault from the Command platform via creating a new certificate store, then the value to enter for the multiple choice options should be "standard,premium".
        If your organization requires that one or the other option should always be used, you can limit the options to a single value ("premium" or "standard").  If not selected, "standard" is used when creating a new KeyVault.

     [^vaultregion]: The Vault Region field is only important when creating a new Azure KeyVault from the Command Platform.  This is the region that the newly created vault will be created in.  When creating the cert store type,
        you can limit the options to those that should be applicable to your organization. Refer to the [Azure Documentation](https://learn.microsoft.com/en-us/dotnet/api/azure.core.azurelocation?view=azure-dotnethttps://learn.microsoft.com/en-us/dotnet/api/azure.core.azurelocation?view=azure-dotnet) for a list of valid region names.
        If no value is selected, "eastus" is used by default.

### Install the Extension on the Orchestrator

The process for installing an extension for the universal orchestrator differs from the process of installing an extension for the Windows orchestrator.  Follow the below steps to register the Azure Keyvault integration with your instance of the universal orchestrator.

1) Stop the Universal Orchestrator service.

     1) Note: In Windows, this service is called "Keyfactor Orchestrator Service (Default)"

1) Create a folder in the "extensions" folder of the Universal Orchestrator installation folder named "AKV" (the name is not important)

     1) example: `C:\Program Files\Keyfactor\Keyfactor Orchestrator\\_AKV_

1) Copy the build output (if you compiled from source) or the contents of the zip file (if you downloaded the pre-compiled binaries) into this folder.

1) Start the Universal Orchestrator Service

### Discover Certificate Stores

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
  - `User` should be set to the managed user ID.
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

> :warning:
> If you are using a system assigned managed identity, you will need to enter the **Tenant Id** value into the "Directories to Search" field.

1) Leave the remaining fields blank and click "SAVE".

#### Approve the Certificate Store

When the Discovery job runs successfully, it will list the existing Azure Keyvaults that are acessible by our service principal.

In this example, our job returned four Azure Keyvaults.

![Discovery Results](/Images/discovery-result.png)

The store path of each vault is the Azure Resource Identifier, and contains the following information:

![Discovery Results](/Images/storepath.png)

To add one of these results to Keyfactor as a certificate store:

1) Double-click the row that corresponds to the Azure Keyvault in the discovery results (you can also select the row and click "approve").

1) In the dialog window, enter the Vault Name and Resource Group Name from the store path value above.

     ![Approve Cert Store](/Images/approve-cert-store.png)

1) Select a container to store the certificates for this cert store (optional)

1) Select any value for SKU Type and Vault Region.  These values are not used for existing KeyVaults.

1) Click "SAVE".

### Add an individual Azure Keyvault certificate store

You can also add a certificate store that corresponds to an Azure Keyvault individually without the need to run the discovery / approval workflow.
The steps to do this are:

1) Navigate to "Locations > Certificate Stores"

1) Click "ADD"

     ![Approve Cert Store](/Images/cert-store-add-button.png)

1) Enter the values corresponding to the Azure Keyvault instance.

- **Category**: Azure Keyvault
- **Container**: _optional_
- **Client Machine**: If applicable; Tenant Id.  

  - Note: These will only have to be entered once, even if adding multiple certificate stores.
  - Follow the steps [here](#store-the-server-credentials-in-keyfactor) to enter them.

- **Store Path**: This is the Azure Resource Identifier for the Keyvault.  Copied from Azure, or created a new Keyvault (see below).  
- **VaultName**: This is the name of the new or existing Azure Keyvault.
- **ResourceGroupName**: The name of the Azure Resource Group that contains the Keyvault.
- **SKU Type**: This field is only used when creating new vaults in Azure.  Select any value, or leave blank.
- **Vault Region**: This field is also only used when creating new vaults.  Select any value.

If the vault already exists in azure:
The store path can be found by navigating to the existing Keyvault resource in Azure and clicking "Properties" in the left menu.

![Resource Id](/Images/resource-id.png)

If the Keyvault does not exist in Azure, and you would like to create it:

- Enter a value for the store path in the following format:

`/subscriptions/{subscription id}/resourceGroups/{resource group for keyvault}/providers/Microsoft.KeyVault/vaults/{new name}`

- For a non-existing Keyvault that you would like to create in Azure, make sure you have the "Create Certificate Store" box checked.

![Add Vault](/Images/add-vault.png)

---

### License

[Apache](https://apache.org/licenses/LICENSE-2.0)

