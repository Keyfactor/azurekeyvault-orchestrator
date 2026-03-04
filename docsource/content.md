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

## Requirements

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