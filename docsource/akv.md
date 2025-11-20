## Overview

The Azure Keyvault Certificate Store Type is designed to integrate with Microsoft Azure Key Vault, enabling users to
manage and automate the lifecycle of cryptographic certificates stored in Azure Keyvault through Keyfactor Command.
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

> :warning:
> The alias you provide when enrolling a certificate will be used as the certificate name in Azure Keyvault.
> Consequently; [it must _only_ contain alphanumeric characters and hyphens](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftkeyvault).
> If you encounter the error "The request URI contains an invalid name" when attempting to perform an enrollment, it is likely due to the use of disallowed characters in the alias.

