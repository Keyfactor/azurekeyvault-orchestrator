<h1 align="center" style="border-bottom: none">
    Azure Key Vault Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/azurekeyvault-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/azurekeyvault-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/azurekeyvault-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/azurekeyvault-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

The Azure Key Vault Universal Orchestrator extension enables seamless integration between Keyfactor Command and
Microsoft Azure Key Vault. This extension facilitates remote management of cryptographic certificates stored in Azure
Key Vault, ensuring organizational security and compliance requirements are met. With this extension, users can manage
certificates remotely by performing various operations such as inventory, addition, removal, and discovery of
certificates and certificate stores.

Azure Key Vault is a cloud service that provides secure storage for secrets, including cryptographic keys and
certificates. Certificates in Azure Key Vault are utilized to secure communications and safeguard data by managing the
associated cryptographic keys and policies.

Defined Certificate Stores of the Certificate Store Type in Keyfactor Command represent the individual or grouped
certificates managed within a specific remote location, such as Azure Key Vault. Each Certificate Store is configured to
interface with an Azure Key Vault instance, allowing the orchestrator to perform the required certificate management
operations.



## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The Azure Key Vault Universal Orchestrator extension is supported by Keyfactor. If you require support for any issues or have feature request, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com.

> If you want to contribute bug fixes or additional enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the Azure Key Vault Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


### Setup and Configuration

The high level steps required to configure the Azure Keyvault Orchestrator extension are:

1) [Configure client access; permissions and authentication](#configure-the-azure-keyvault-for-client-access)

1) [Create the Store Type in Keyfactor](#AKV-Certificate-Store-Type)

1) [Install the Extension on the Orchestrator](#installation)

1) [Create the Certificate Store](#add-a-new-or-existing-azure-keyvault-certificate-store)

> :warning: If you are still using the (deprecated) Windows Orchestrator, you can find instructions for migrating by searching previous versions of this README.

---

#### Configure the Azure Keyvault for client access

In order for this orchestrator extension to be able to interact with your instances of Azure Keyvault, it will need to
authenticate with a identity that has sufficient permissions to perform the jobs. Microsoft Azure implements both Role
Based Access Control (RBAC) and the classic Access Policy method. RBAC is the preferred method, and currently the default used by Azure.
It allows the assignment of granular level, inheretable access control on both the contents of the KeyVaults, as well as higher-level
management operations. For more information and a comparison of the two access control strategies, refer
to [this article](learn.microsoft.com/en-us/azure/key-vault/general/rbac-access-policy).

##### RBAC vs Access Policies

Azure KeyVaults originally utilized access policies for permissions and since then, Microsoft has begun recommending
Role Based Access Control (RBAC) as the preferred method of authorization.  
New KeyVaults created via this integration are created with the default authorization method that is configured in the Azure environment.

The access control type the KeyVault implements can be changed in the KeyVault configuration within the Azure Portal.
New KeyVaults created via Keyfactor by way of this integration will use the default that is configured in your Azure environment (as of February 2026, if not specified, RBAC).

> :exclamation: Additional guidance regarding the minimum permissions needed for each job and sample RBAC policy definitions with descriptions can be found [here](rbac.md).

#### Endpoint Access / Firewall

At a minimum, the orchestrator needs access to the following URLs:

- The instance of Keyfactor Command
- 'login.microsoftonline.com' (or the endpoint corresponding to the Azure Global Cloud instance (Government, China,
  Germany).
    - this is only technically necessary if they are using Service Principal authentication.
- 'management.azure.com' for all management operations (Create, Add, Remove) as well as Discovery.
    - This is necessary for authenticating the ARM client used to perform these operations.

Any firewall applied to the orchestrator host will need to be configured to allow access to these endpoints in order for
this integration to make the necessary API requests.

> :warning: Discovery jobs are not supported for KeyVaults located outside of the Azure Public cloud or Keyvaults
> accessed via a private url endpoint.  
> All other job types implemented by this integration are supported for alternate Azure clouds and private endpoints.

#### Authentication options

The Azure KeyVault orchestrator plugin supports several authentication options:

- [Service Principal](#authentication-via-service-principal)
- [User Assigned Managed Identities](#authentication-via-user-assigned-managed-identity)
- [System Assigned Managed Identities](#authentication-via-system-assigned-managed-identity)

Steps for setting up each option are detailed below.

<details>
  <summary><b>Authentication via Service Principal</b></summary>

For the Orchestrator to be able to interact with the instance of Azure Keyvault, we will need to create an entity in
Azure that will encapsulate the permissions we would like to grant it. In Azure, these intermediate entities are
referred to as app registrations and they provision authority for external application access.
To learn more about application and service principals, refer
to [this article](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal).

To provision access to the Keyvault instance using a service principal identity, we will:

1) [Create a Service Principal in Azure Active Directory](#create-a-service-principal)

1) [Assign it sufficient permissions for Keyvault operations](#assign-permissions)

1) [Generate an Access Token for Authenticating](#generate-an-access-token)

1) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

**In order to complete these steps, you must have the _Owner_ role for the Azure subscription, at least temporarily.**
This is required to create an App Registration in Azure Active Directory.

#### Create A Service Principal

**Note:** In order to manage key vaults in multiple Azure tenants using a single service principal, the supported
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

We will store these values securely in Keyfactor in subsequent steps.

</details>

<details>
  <summary><b>Authentication via User Assigned Managed Identity</b></summary>

Authentication has been somewhat simplified with the introduction of Azure Managed Identities. If the orchestrator is
running on an Azure Virtual Machine, Managed identities allow an Azure administrator to
assign a managed identity to the virtual machine that can then be used by this orchestrator extension for authentication
without the need to issue or manage client secrets.

The two types of managed identities available in Azure are _System_ assigned, and _User_ assigned identities.

- System assigned managed identities are bound to the specific resource and not reassignable. They are bound to the
  resource and share the same lifecycle.
- User assigned managed identities exist as a standalone entity, independent of a resource, and can therefore be
  assigned to multiple Azure resources.

Read more about Azure Managed
Identities [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview).

Detailed steps for creating a managed identity and assigning permissions can be
found [here](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).

Once the User Assigned managed identity has been created, you will need only to enter the Client Id into the Application
Id field on the certificate store definition (the Client Secret can be left blank).

</details>

<details>
<summary><b>Authentication via System Assigned Managed Identity</b></summary>

In order to use a _System_ assigned managed identity, there is no need to enter the server credentials. If no server
credentials are provided, the extension assumes authentication is via system assigned managed identity.

</details>

### Running a Discovery Job

Now that we have the extension registered on the Orchestrator, we can navigate back to the Keyfactor platform and finish
the setup. If there are existing Azure Key Vaults, complete the below steps to discover and add them. If there are no
existing key vaults to integrate and you will be creating a new one via the Keyfactor Platform, you can skip to the next
section.

1) Navigate to Orchestrators > Management in the platform.

   ![Manage Orchestrators](/Images/orch-manage.png)

1) Find the row corresponding to the orchestrator that we just installed the extension on.

1) If the store type has been created and the integration installed on the orchestrator, you should see the _AKV_
   capability in the list.

   ![AKV Capability](/Images/akv-capability.png)

1) Approve the orchestrator if necessary.

##### Create the discovery job

1) Navigate to "Locations > Certificate Stores"

   ![Locations Cert Stores](/Images/locations-certstores.png)

1) Click the "Discover" tab, and then the "Schedule" button.

   ![Discovery Schedule](/Images/discover-schedule.png)

1) You should see the form for creating the Discovery job.

   ![Discovery Form](/Images/discovery-form.png)

##### Store the Server Credentials in Keyfactor

> :warning:
> The steps for configuring discovery are different for each authentication type.

- For System Assigned managed identity authentication this step can be skipped. No server credentials are necessary. The
  store type should have been set up without "needs server" checked, so the form field should not be present.

- For User assigned managed identity:
    - `Client Machine` should be set to the GUID of the tenant ID of the instance of Azure Keyvault.
    - `User` should be set to the Client ID of the managed identity.
    - `Password` should be set to the value **"managed"**.

- For Service principal authentication:
    - `Client Machine` should be set to the GUID of the tenant ID of the instance of Azure Keyvault. **Note:** If using
      a multi-tenant app registration, use the tenant ID of the Azure tenant where the key vault lives.
    - `User` should be set to the service principal id
    - `Password` should be set to the client secret.

The first thing we'll need to do is store the server credentials that will be used by the extension.
The combination of fields required to interact with the Azure Keyvault are:

- Tenant (or Directory) ID
- Application ID or user managed identity ID
- Client Secret (if using Service Principal Authentication)

If not using system managed identity authentication, the integration expects the above values to be included in the
server credentials in the following way:

- **Client Machine**: `<tenantId>` (GUID)

- **User**: `<app id guid>` (if service principal authentication) `<managed user id>` (if user managed identity
  authentication is used)

- **Password**: `<client secret>` (if service principal authentication), `managed` (if user managed identity
  authentication is used)

Follow these steps to store the values:

1) Enter the _Tenant Id_ in the **Client Machine** field.

   ![Discovery Form](/Images/discovery-form-client-machine.png)

1) Click "Change Credentials" to open up the Server Credentials form.

   ![Change Credentials](/Images/change-credentials-form.png)

1) Click "UPDATE SERVER USERNAME" and Enter the appropriate values based on the authentication type.

   ![Set Username](/Images/server-creds-username.png)

1) Enter again to confirm, and click save.

1) Click "UPDATE SERVER PASSWORD" and update with the appropriate value (`<client secret>` or `managed`) following the
   same steps as above.

1) Select a time to run the discovery job.

1) Enter commma seperated list of tenant ID's in the "Directories to search" field.'

> :warning:
> If nothing is entered here, the default Tenant ID included with the credentials will be used. For system managed
> identities, it is necessary to include the Tenant ID(s) in this field.

1) Leave the remaining fields blank and click "SAVE".

##### Approve the Certificate Store

When the Discovery job runs successfully, it will list the existing Azure Keyvaults that are acessible by our service
principal.

In this example, our job returned these Azure Keyvaults.

![Discovery Results](/Images/discovery-result.png)

The store path of each vault is the `<subscription id>:<resource group name>:<vault name>`:

![Discovery Results](/Images/storepath.png)

To add one of these results to Keyfactor as a certificate store:

1) Double-click the row that corresponds to the Azure Keyvault in the discovery results (you can also select the row and
   click "SAVE").

1) In the dialog window, enter values for any of the optional fields you have set up for your store type.

1) Select a container to store the certificates for this cert store (optional)

1) Select any value for SKU Type and Vault Region. These values are not used for existing KeyVaults.

1) Click "SAVE".

### Add an existing Azure Keyvault certificate store

You can also add a certificate store that corresponds to an Azure Keyvault individually without the need to run the
discovery / approval workflow.
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

- **Store Path**: This is the Subscription ID, Resource Group name, and Vault name in the following format:
  `{subscription id}:{resource group name}:{new vault name}`

- **SKU Type**: This field is only used when creating new vaults in Azure. If present, select any value, or leave blank.
- **Vault Region**: This field is also only used when creating new vaults. If present, select any value.

If the vault already exists in azure the store path can be found by navigating to the existing Keyvault resource in
Azure and clicking "Properties" in the left menu.

![Resource Id](/Images/resource-id.png)

- Use these values to create the store path
- Save the certificate store
- If an inventory schedule was provided, a new inventory job should appear in _Orchestrators > Jobs_.

### Create a new Azure Keyvault

- Enter a value for the store path in the following format: `{subscription id}:{resource group name}:{new vault name}`
- Make sure that the "Create Certificate Store" box is checked.
- Optionally choose values for the **SKUtype**, **Vault Region**, **Azure Cloud** and **Private Endpoint** (as applicable).
  - The **SKUType** and **Vault Region** fields are _only_ used when creating new KeyVaults.
- Save the certificate store
-Navigate to _Orchestrators > Jobs_; you should see the "Management" job that was generated in order to create the Keyvault.
- Once this job completes, a new Keyvault should have been created 

> :warning: The identity you are using for authentication will need to have sufficient Azure permissions to be able to
> create new Keyvaults.

---


## AKV Certificate Store Type

To use the Azure Key Vault Universal Orchestrator extension, you **must** create the AKV Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.



The Azure Keyvault Certificate Store Type is designed to integrate with Microsoft Azure Key Vault, enabling users to
manage and automate the lifecycle of cryptographic certificates stored in Azure Key Vault through Keyfactor Command.
This Certificate Store Type represents the connection and configuration necessary to interact with specific instances of
Azure Key Vault, allowing for operations such as inventory, addition, removal, and discovery of certificates and
certificate stores.

This integration leverages Azure's robust security infrastructure, utilizing OAuth-based authentication methods
including Service Principals, User Assigned Managed Identities, and System Assigned Managed Identities. This ensures
that only authorized entities can manage the certificates stored within the Key Vault.

While this Certificate Store Type provides a powerful means of managing certificates, there are some important caveats
to consider. For example, if your instance of Azure Key Vault utilizes private or custom endpoints, or is hosted outside
of the Azure Public cloud (e.g., Government, China, Germany instances), certain functions like discovery job
functionality may not be supported. Additionally, the configuration of access control through Azure's Role Based Access
Control (RBAC) or classic Access Policies must be meticulously managed to ensure sufficient permissions for the
orchestrator to perform its tasks.

The integration does not require a specific SDK, as it interacts with Azure services directly through their APIs.
However, ensuring that the orchestrator has network access to Azure endpoints is crucial for smooth operation. Being
mindful of these caveats and limitations will help ensure successful deployment and use of the Azure Keyvault
Certificate Store Type within your organization’s security framework.




#### Supported Operations

| Operation    | Is Supported                                                                                                           |
|--------------|------------------------------------------------------------------------------------------------------------------------|
| Add          | ✅ Checked        |
| Remove       | ✅ Checked     |
| Discovery    | ✅ Checked  |
| Reenrollment | 🔲 Unchecked |
| Create       | ✅ Checked     |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to create certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)
   <details><summary>Click to expand AKV kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # Azure Keyvault
   kfutil store-types create AKV
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>


#### Manual Creation
Below are instructions on how to create the AKV store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual AKV details</summary>

   Create a store type called `AKV` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | Azure Keyvault | Display name for the store type (may be customized) |
   | Short Name | AKV | Short display name for the store type |
   | Capability | AKV | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
   | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
   | Supports Discovery | ✅ Checked | Check the box. Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
   | Supports Create | ✅ Checked | Check the box. Indicates that the Store Type supports store creation |
   | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![AKV Basic Tab](docsource/images/AKV-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AKV Advanced Tab](docsource/images/AKV-advanced-store-type-dialog.png)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | TenantId | Tenant Id | The ID of the primary Azure Tenant where the KeyVaults are hosted | String |  | 🔲 Unchecked |
   | SkuType | SKU Type | The SKU type for newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) | MultipleChoice | standard,premium | 🔲 Unchecked |
   | VaultRegion | Vault Region | The Azure Region to put newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) | MultipleChoice | eastus,eastus2,westus2,westus3,westus | 🔲 Unchecked |
   | AzureCloud | Azure Cloud | The Azure Cloud where the KeyVaults are located (only necessary if not using the standard Azure Public cloud) | MultipleChoice | public,china,government | 🔲 Unchecked |
   | PrivateEndpoint | Private KeyVault Endpoint | The private endpoint of your vault instance (if a private endpoint is configured in Azure) | String |  | 🔲 Unchecked |

   The Custom Fields tab should look like this:

   ![AKV Custom Fields Tab](docsource/images/AKV-custom-fields-store-type-dialog.png)


   ###### Tenant Id
   The ID of the primary Azure Tenant where the KeyVaults are hosted

   ![AKV Custom Field - TenantId](docsource/images/AKV-custom-field-TenantId-dialog.png)
   ![AKV Custom Field - TenantId](docsource/images/AKV-custom-field-TenantId-validation-options-dialog.png)



   ###### SKU Type
   The SKU type for newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command)

   ![AKV Custom Field - SkuType](docsource/images/AKV-custom-field-SkuType-dialog.png)
   ![AKV Custom Field - SkuType](docsource/images/AKV-custom-field-SkuType-validation-options-dialog.png)



   ###### Vault Region
   The Azure Region to put newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command)

   ![AKV Custom Field - VaultRegion](docsource/images/AKV-custom-field-VaultRegion-dialog.png)
   ![AKV Custom Field - VaultRegion](docsource/images/AKV-custom-field-VaultRegion-validation-options-dialog.png)



   ###### Azure Cloud
   The Azure Cloud where the KeyVaults are located (only necessary if not using the standard Azure Public cloud)

   ![AKV Custom Field - AzureCloud](docsource/images/AKV-custom-field-AzureCloud-dialog.png)
   ![AKV Custom Field - AzureCloud](docsource/images/AKV-custom-field-AzureCloud-validation-options-dialog.png)



   ###### Private KeyVault Endpoint
   The private endpoint of your vault instance (if a private endpoint is configured in Azure)

   ![AKV Custom Field - PrivateEndpoint](docsource/images/AKV-custom-field-PrivateEndpoint-dialog.png)
   ![AKV Custom Field - PrivateEndpoint](docsource/images/AKV-custom-field-PrivateEndpoint-validation-options-dialog.png)





   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | CertificateTags | Certificate Tags | If desired, tags can be applied to the KeyVault entries.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}' | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | PreserveExistingTags | Preserve Existing Tags | If true, this will perform a union of any tags provided with enrollment with the tags on the existing cert with the same alias and apply the result to the new certificate. | Bool | False | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | NonExportable | Non Exportable Private Key | If true, this will mark the certificate as having a non-exportable private key when importing into Azure KeyVault | Bool | False | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AKV Entry Parameters Tab](docsource/images/AKV-entry-parameters-store-type-dialog.png)


   ##### Certificate Tags
   If desired, tags can be applied to the KeyVault entries.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}'

   ![AKV Entry Parameter - CertificateTags](docsource/images/AKV-entry-parameters-store-type-dialog-CertificateTags.png)
   ![AKV Entry Parameter - CertificateTags](docsource/images/AKV-entry-parameters-store-type-dialog-CertificateTags-validation-options.png)


   ##### Preserve Existing Tags
   If true, this will perform a union of any tags provided with enrollment with the tags on the existing cert with the same alias and apply the result to the new certificate.

   ![AKV Entry Parameter - PreserveExistingTags](docsource/images/AKV-entry-parameters-store-type-dialog-PreserveExistingTags.png)
   ![AKV Entry Parameter - PreserveExistingTags](docsource/images/AKV-entry-parameters-store-type-dialog-PreserveExistingTags-validation-options.png)


   ##### Non Exportable Private Key
   If true, this will mark the certificate as having a non-exportable private key when importing into Azure KeyVault

   ![AKV Entry Parameter - NonExportable](docsource/images/AKV-entry-parameters-store-type-dialog-NonExportable.png)
   ![AKV Entry Parameter - NonExportable](docsource/images/AKV-entry-parameters-store-type-dialog-NonExportable-validation-options.png)



   </details>

## Installation

1. **Download the latest Azure Key Vault Universal Orchestrator extension from GitHub.**

    Navigate to the [Azure Key Vault Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/azurekeyvault-orchestrator/releases/latest). Refer to the compatibility matrix below to determine the asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `azurekeyvault-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Older than `11.0.0` | | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` || Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` | 

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`

3. **Create a new directory for the Azure Key Vault Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `azurekeyvault-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `azurekeyvault-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration**

    The Azure Key Vault Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores



### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description                                             |
   | --------- |---------------------------------------------------------|
   | Category | Select "Azure Keyvault" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | The GUID of the tenant ID of the Azure Keyvault instance; for example, '12345678-1234-1234-1234-123456789abc'. |
   | Store Path | A string formatted as '{subscription id}:{resource group name}:{vault name}'; for example, '12345678-1234-1234-1234-123456789abc:myResourceGroup:myVault'. |
   | Orchestrator | Select an approved orchestrator capable of managing `AKV` certificates. Specifically, one with the `AKV` capability. |
   | TenantId | The ID of the primary Azure Tenant where the KeyVaults are hosted |
   | SkuType | The SKU type for newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) |
   | VaultRegion | The Azure Region to put newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) |
   | AzureCloud | The Azure Cloud where the KeyVaults are located (only necessary if not using the standard Azure Public cloud) |
   | PrivateEndpoint | The private endpoint of your vault instance (if a private endpoint is configured in Azure) |

</details>



#### Using kfutil CLI

<details><summary>Click to expand details</summary>

1. **Generate a CSV template for the AKV certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name AKV --outpath AKV.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "Azure Keyvault" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | The GUID of the tenant ID of the Azure Keyvault instance; for example, '12345678-1234-1234-1234-123456789abc'. |
   | Store Path | A string formatted as '{subscription id}:{resource group name}:{vault name}'; for example, '12345678-1234-1234-1234-123456789abc:myResourceGroup:myVault'. |
   | Orchestrator | Select an approved orchestrator capable of managing `AKV` certificates. Specifically, one with the `AKV` capability. |
   | Properties.TenantId | The ID of the primary Azure Tenant where the KeyVaults are hosted |
   | Properties.SkuType | The SKU type for newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) |
   | Properties.VaultRegion | The Azure Region to put newly created KeyVaults (only needed if needing to create new KeyVaults in your Azure subscription via Command) |
   | Properties.AzureCloud | The Azure Cloud where the KeyVaults are located (only necessary if not using the standard Azure Public cloud) |
   | Properties.PrivateEndpoint | The private endpoint of your vault instance (if a private endpoint is configured in Azure) |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name AKV --file AKV.csv
    ```

</details>


#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | ServerUsername | Username to use when connecting to server |
   | ServerPassword | Password to use when connecting to server |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>


> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).





## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).