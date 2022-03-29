# Azure Keyvault

## Orchestrator Extension

The Azure KeyVault Orchestrator allows for management of certificates within an Azure Key Vault. Discovery, Inventory and Management functions are supported.

## About the Keyfactor Azure Keyvault Integration

This integration allows the orchestrator to act as a client with access to an instance of the Azure Key Vault; allowing you to manage your certificates stored in the Azure KeyVault via Keyfactor.

---

## Setup and Configuration

The high level steps required to configure the Azure Keyvault Orchestrator extension are

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
<img src="/Images/app-registration.PNG" width="200">

1) Once the entity has been created, you should be directed to the overview view.
![App Registration Overview](/Images/app-registration-overview.png)
1) Copy the _Application (client) ID_, _Object ID_, and _Directory (tenant) ID_.  We will need these later.


#### Assign Permissions
Now that we have created our app registration / service principle entity, we need to make sure it has sufficient permissions for certificate operations.
In order to be able to perform certificate operations on the Keyvault, we will have to assign Certificate Permissions to the Application Entity that we created above.

1) Navigate to the Azure Portal and then to your instance of the Azure Keyvault.
1) Go to "Access Policies" in the navigation menu for the Key vault.
1) Click "+ Add Access Policy"
1) In the first drop-down, you can select "Certificate Management".  This will select all certificate management permissions.
![Permission List](/Images/cert-mgmt-perm-list.png)
1) Click "Select Principal" to open the search pane.
1) Find the Application Registration we created above, select it, and click "Select".
![Select Principal](/Images/select-principal.png)
1) Leave "Authorized application" unselected.
1) Click "Add".
1) After you are redirected to the "Access policies" view, you should see the App Registration listed under "APPLICATION".
1) Click "Save" at the top of this view.
![Select Principal](/Images/save-access-policy.png)

#### Generate an Access Token



---

### Create the Store Type in Keyfactor

---

### Install the Extension on the Orchestrator


---

### Create the Certificate Store

The following are the parameter names and a description of the values needed to configure the Azure Keyvault Orchestrator Extension.

| Initialization parameter | Description | 
| :---: | --- | :---: | --- |
| Client Machine | This should be the Azure Subscription Id 
| APIObjectId | The base64 encode API registration key from BeyondTrust | AccountID | The ID number of the account on the system, whose password will be retrieved |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |
| Username | The username that the API request will be run as. This user needs to have sufficient permissions on the API key and the credentials to request |