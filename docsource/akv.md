## Overview

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

## Discovery Job Configuration

1) Navigate to Orchestrators > Management in the platform.

   ![Manage Orchestrators](/Images/orch-manage.png)

2) Find the row corresponding to the orchestrator that we just installed the extension on.

3) If the store type has been created and the integration installed on the orchestrator, you should see the _AKV_
   capability in the list.

   ![AKV Capability](/Images/akv-capability.png)

4) Approve the orchestrator if necessary.

### Create the discovery job

1) Navigate to "Locations > Certificate Stores"

   ![Locations Cert Stores](/Images/locations-certstores.png)

2) Click the "Discover" tab, and then the "Schedule" button.

   ![Discovery Schedule](/Images/discover-schedule.png)

3) You should see the form for creating the Discovery job.

   ![Discovery Form](/Images/discovery-form.png)

### Store the Server Credentials in Keyfactor

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

2) Click "Change Credentials" to open up the Server Credentials form.

   ![Change Credentials](/Images/change-credentials-form.png)

3) Click "UPDATE SERVER USERNAME" and Enter the appropriate values based on the authentication type.

   ![Set Username](/Images/server-creds-username.png)

4) Enter again to confirm, and click save.

5) Click "UPDATE SERVER PASSWORD" and update with the appropriate value (`<client secret>` or `managed`) following the
   same steps as above.

6) Select a time to run the discovery job.

7) Enter commma seperated list of tenant ID's in the "Directories to search" field.'

> :warning:
> If nothing is entered here, the default Tenant ID included with the credentials will be used. For system managed
> identities, it is necessary to include the Tenant ID(s) in this field.

1) Leave the remaining fields blank and click "SAVE".

### Approve the Certificate Store

When the Discovery job runs successfully, it will list the existing Azure Keyvaults that are acessible by our service
principal.

In this example, our job returned these Azure Keyvaults.

![Discovery Results](/Images/discovery-result.png)

The store path of each vault is the `<subscription id>:<resource group name>:<vault name>`:

![Discovery Results](/Images/storepath.png)

To add one of these results to Keyfactor as a certificate store:

1) Double-click the row that corresponds to the Azure Keyvault in the discovery results (you can also select the row and
   click "SAVE").

2) In the dialog window, enter values for any of the optional fields you have set up for your store type.

3) Select a container to store the certificates for this cert store (optional)

4) Select any value for SKU Type and Vault Region. These values are not used for existing KeyVaults.

5) Click "SAVE".

### Add a new or existing Azure Keyvault certificate store

You can also add a certificate store that corresponds to an Azure Keyvault individually without the need to run the
discovery / approval workflow.
The steps to take this are:

1) Navigate to "Locations > Certificate Stores"

2) Click "ADD"

   ![Approve Cert Store](/Images/cert-store-add-button.png)

3) Enter the values corresponding to the Azure Keyvault instance.

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

If the Keyvault does not exist in Azure, and you would like to create it:

- Enter a value for the store path in the following format: `{subscription id}:{resource group name}:{new vault name}`

- For a non-existing Keyvault that you would like to create in Azure, make sure you have the "Create Certificate Store"
  box checked.

> :warning: The identity you are using for authentication will need to have sufficient Azure permissions to be able to
> create new Keyvaults.

---

