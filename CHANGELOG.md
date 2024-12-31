- 3.1.6
  - Preventing CertStore parameters from getting used if present but empty. 	
  - Improved trace logging
  - Convert to .net6/8 dual build
  - Update README to use doctool
	 
- 3.1.5
  - Bug fix for error when adding new cert and overwrite is unchecked
	
- 3.1.4
  - Update nuget dependencies (Azure Identity Packages)

- 3.1.3
  - Discovery now continues the search if an error is encountered during the process. 
  - Fixed issue with overwrite box check being ignored when replacing cert in Keyvault
  -	Now getting properties of certs as pageable during inventory to fix a timeout issue when querying for thousands of certs.

- 3.1.2
  - Fixed bug that was preventing the full certificate chain from being sent to the Azure Keyvault API endpoint. 

- 3.1.1
  - Updated documentation to clarify required orchestrator access.

- 3.1.0
  - Updated the Discovery job to support multiple tenants and all accessible subscriptions they contain
  - Added more detailed trace logging during the discovery process
  - Changed store path to be `subscription id : resource group name : vault name`
  - Removed redundant Vault Name and Resource Group Name fields
  - Updated documentation to explain when optional fields can be omitted from the store type definition
  - Added support for legacy store paths on existing stores

- 3.0
  - Added support for Azure clouds other than US public.
  - Shortened Store path to `subscription id : vault name`
  - Added PAM support

- 2.0.1
  - Fixed bug when trying to parse Vault plan (premium/standard)

- 2.0
  - Added support for Azure Managed Identity authentication
  - Updated Azure client libraries
  - Removed ObjectId parameter from StoreType definition
  - Added SkuType and VaultRegion parameters to support vault creation from the platform

- 1.05
