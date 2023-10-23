
- 3.1
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
