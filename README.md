# Azure Keyvault

## Orchestrator Extension

The Azure KeyVault Orchestrator allows for management of certificates within an Azure Key Vault. Discovery, Inventory and Management functions are supported.

## About the Keyfactor Azure Keyvault Integration

This integration allows the orchestrator to act as a client with access to an instance of the Azure Key Vault; allowing you to manage your certificates stored in the Azure KeyVault via Keyfactor.

---

## Setup and Configuration

The high level steps required to configure the Azure Keyvault Orchestrator extension are:

1) [Configure the Azure KeyVault for client access](#configure-the-azure-keyvault-for-client-access)

1) [Create the Store Type in Keyfactor](#create-the-store-type-in-keyfactor)

1) [Install the Extension on the Orchestrator](#install-the-extension-on-the-orchestrator)

1) [Create the Certificate Store](#create-the-certificate-store)

---

### Configure the Azure KeyVault for client access

To provision access to the KeyVault instance, we will:

1) [Create a Service Principle in Azure Active Directory](#create-a-service-principle)

1) [Assign it sufficient permissions for KeyVault operations](#assign-permissions)

1) [Generate an Access Token for Authenticating](#generate-an-access-token)

1) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

#### Create a Service Principle

For the Orchestrator to be able to interact with the instance of Azure KeyVault, we will need to create an entity in Azure that will encapsulate the permissions we would like to grant it.  In Azure, these intermediate entities are referred to as app registrations and they provision authority for external application access.
To learn more about application and service principals, refer to [this article](https://docs.microsoft.com/en-us/azure/active-directory/develop/howto-create-service-principal-portal).

**In order to complete these steps, you must have the _Owner_ role for the Azure subscription, at least temporarily.**
This is required to create an App Registration in Azure Active Directory.

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

1) Copy the _Application (client) ID_, _Object ID_.

1) Now we have a App registration and values for  _Directory (tenant) ID_, _Application (client) ID_ and _Object ID_.  These will be used by the integration for authentication to Azure.

#### Assign Permissions

In order to be able to discover and create new Azure KeyVault certificate stores, the app principal that we created must be provided with the "KeyVault Administrator" role at the _Resource Group_ level.[^1]
_If there are multiple resource groups that will contain Key Vaults to be managed, you should repeat for each._

Here are the steps for assigning this role.

1) Navigate to the Azure portal and select a resource group that will contain the KeyVaults we would like to manage.
1) Select "Access control (IAM)" from the left menu.
1) Click "Add", then "Add Role Assignment" to create a new role assignment

     ![Resource Group Add Role](/Images/resource-group-add-role.PNG)
1) Search and Select the "Key Vault Administrator" role.
1) Search and Select the principal we created.

     ![Select Principal](/Images/rg-role-select-principal.PNG)
1) Click "Review and Assign" and save the role assignment.

[^1]: If discovery and create store functionality are not neeeded, it is also possible to manage individual certificate stores without the need to provide resource group level authority.  The steps to do assign permissions for an individual Azure Keyvault are described [here].(#assign-permissions-for-an-individual-key-vault).

#### Assign Permissions for an Individual Key Vault

Following the below steps will provide our service principal with the ability to manage keys in an existing vault, without providing it the elevated permissions required for discovering existing vaults or creating new ones.  If you've completed the steps in the previous section for the resource group that contains the Key Vault(s) you would like to manage, the below steps are not necessary.

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

For authenticating to Azure via our App Registration, we will need to generate an access token.

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
- _ObjectId_
- _ClientSecret_

We will store these values securely in Keyfactor in subsequent steps.

### Create the Store Type in Keyfactor

Now we can navigate to the Keyfactor platform and create the store type for Azure Key Vault.

1) Navigate to your instance of Keyfactor and log in with a user that has Administrator priveledges.

1) Click on the gear icon in the top left and navigate to "Certificate Store Types".

     ![Cert Store Types Menu](/Images/cert-store-types-menu.png)

1) Click "Add" to open the Add Certificate Store dialog.

1) Name the new store type "Azure KeyVault" and give it the short name of "AKV".

1) The Azure KeyVault integration supports the following job types: _Inventory, Add, Remove, Create and Discovery_.  Select from these the capabilities you would like to utilize.

1) Make sure that "Needs Server" is checked.

     ![Cert Store Types Menu](/Images/cert-store-type.png)

1) Navigate to the _Custom Fields_ tab and add the following fields
     - Vault Name (VaultName) - _required_
     - Resource Group Name (ResourceGroupName) - _required_

### Install the Extension on the Orchestrator

The process for installing an extension for the universal orchestrator differs from the process of installing an extension for the Windows orchestrator.  Follow the below steps to register the Azure KeyVault integration with your instance of the universal orchestrator.

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

#### Run the discovery job

1) Navigate to "Locations > Certificate Stores"

     ![Locations Cert Stores](/Images/locations-certstores.png)

1) Click the "Discover" tab, and then the "Schedule" button.

     ![Discovery Schedule](/Images/discover-schedule.png)

#### Store the Server Credentials in Keyfactor

### Create the Certificate Store

<!-- 
The following are the parameter names and a description of the values needed to configure the Azure Keyvault Orchestrator Extension.

| Initialization parameter | Description |
| :---: | --- | :---: | --- |
| Client Machine | This should be the Azure Subscription Id
| APIObjectId | The base64 encode API registration key from BeyondTrust | AccountID | The ID number of the account on the system, whose password will be retrieved |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request | -->
