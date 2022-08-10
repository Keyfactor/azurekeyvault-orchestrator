## Setup and Configuration

The high level steps required to configure the Azure Keyvault Orchestrator extension are:

1) [Configure the Azure Keyvault for client access](#configure-the-azure-keyvault-for-client-access)

1) [Create the Store Type in Keyfactor](#create-the-store-type-in-keyfactor)

1) [Install the Extension on the Orchestrator](#install-the-extension-on-the-orchestrator)

1) [Create the Certificate Store](#create-the-certificate-store)

---

### Configure the Azure Keyvault for client access

To provision access to the Keyvault instance, we will:

1) [Create a Service Principle in Azure Active Directory](#create-a-service-principle)

1) [Assign it sufficient permissions for Keyvault operations](#assign-permissions)

1) [Generate an Access Token for Authenticating](#generate-an-access-token)

1) [Store the server credentials in Keyfactor](#store-the-server-credentials-in-keyfactor)

#### Create a Service Principle

For the Orchestrator to be able to interact with the instance of Azure Keyvault, we will need to create an entity in Azure that will encapsulate the permissions we would like to grant it.  In Azure, these intermediate entities are referred to as app registrations and they provision authority for external application access.
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

[^1]: If discovery and create store functionality are not neeeded, it is also possible to manage individual certificate stores without the need to provide resource group level authority.  The steps to do assign permissions for an individual Azure Keyvault are described [here](#assign-permissions-for-an-individual-key-vault).

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

1) Name the new store type "Azure Keyvault" and give it the short name of "AKV".

1) The Azure Keyvault integration supports the following job types: _Inventory, Add, Remove, Create and Discovery_.  Select from these the capabilities you would like to utilize.

1) Make sure that "Needs Server" is checked.

     ![Cert Store Types Menu](/Images/cert-store-type.png)

1) Navigate to the _Custom Fields_ tab and add the following fields
     - Vault Name (VaultName) - _required_
     - Resource Group Name (ResourceGroupName) - _required_

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

The first thing we'll need to do is store the server credentials that will be used by the extension.
The combination of fields required to interact with the Azure Keyvault are:

- Subscription ID
- Tenant (or Directory) ID
- Application ID (of the service principal)
- Object ID (of the service principal)
- Client Secret

This integration expects the above values to be included in the server credentials in the following way:

- **Client Machine**: `<subscription id>` (GUID)

- **User**: `<tenantId> <app id guid> <object Id>` (GUID's separated by spaces)

- **Password**: `<client secret>`

Follow these steps to store the values:

1) Enter the _Subscription ID_ in the **Client Machine** field.

     ![Discovery Form](/Images/discovery-form-client-machine.png)

1) Click "Change Credentials" to open up the Server Credentials form.

     ![Change Credentials](/Images/change-credentials-form.png)

1) Click "UPDATE SERVER USERNAME" and Enter the three GUIDs corresponding to **Tenant ID**, **App ID** and **Object ID** for the Server Username.  
     example: `c9ed4b45-9f70-418a-aa58-f04c80848ca9 6e5de0a7-318a-46ed-ba46-a62b3ff28f55 5261a31a-5d8c-4d1e-93d0-bec81a786f75`

      ![Set Username](/Images/server-creds-username.png)

1) Enter again to confirm, and click save.

1) Click "UPDATE SERVER PASSWORD" and update the value with the **Client Secret** following the same steps as above.

1) Select a time to run the discovery job.

1) Enter a comma-separated list of resource group names if you would like to limit the discovery process to a subset of resource groups.  Otherwise enter "AKV" into the **Directories to Search** field.  

1) Leave the remaining fields blank and click "DONE".

#### Approve the Certificate Store

When the Discovery job runs successfully, it will list the existing Azure Keyvaults that are acessible by our service principle.

In this example, our job returned four Azure Keyvaults.

![Discovery Results](/Images/discovery-result.png)

The store path of each vault is the Azure Resource Identifier, and contains the following information:

![Discovery Results](/Images/storepath.png)

To add one of these results to Keyfactor as a certificate store:

1) Double-click the row that corresponds to the Azure Keyvault in the discovery results (you can also select the row and click "approve").

1) In the dialog window, enter the Vault Name and Resource Group Name from the store path value above.

     ![Approve Cert Store](/Images/approve-cert-store.png)

1) Select a container to store the certificates for this cert store (optional)

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
- **Client Machine**: Azure Subscription Id.  

  - Note: These will only have to be entered once, even if adding multiple certificate stores.
  - Follow the steps [here](#store-the-server-credentials-in-keyfactor) to enter them.

- **Store Path**: This is the Azure Resource Identifier for the Keyvault.  Copied from Azure, or created a new Keyvault (see below).  
- **VaultName**: This is the name of the new or existing Azure Keyvault.
- **ResourceGroupName**: The name of the Azure Resource Group that contains the Keyvault.

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
