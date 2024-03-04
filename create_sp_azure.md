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

[Back to README](README.md#create-a-service-principal)
 

