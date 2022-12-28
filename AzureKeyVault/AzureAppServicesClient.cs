using System.Collections.Generic;
using System.Linq;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Certificates;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AzureAppServicesClient
    {
        // Environment variables required to authenticate using DefaultAzureCredential:
        // - AZURE_CLIENT_ID → The app ID value.
        // - AZURE_TENANT_ID → The tenant ID value.
        // - AZURE_CLIENT_SECRET → The password/credential generated for the app.

        // The methods in this class mostly return lists because certificate resources representing the same certificate
        // can exist in multiple resource groups. More specifically, App Service Certificate resources are bound to
        // a specific resource group and region combination. This means that if a certificate DNS hostname list matches
        // hostnames in multiple resource groups, the certificate needs to be imported and bound multiple times.

        public AzureAppServicesClient(AkvProperties properties)
        {
            // Each AzureAppServicesClient represents a single resource group (and therefore subscription).

            Log = LogHandler.GetClassLogger<AzureAppServicesClient>();
            Log.LogDebug("Initializing Azure App Services client");

            string subscriptionId = string.IsNullOrEmpty(properties.SubscriptionId)
                ? new ResourceIdentifier(properties.StorePath).SubscriptionId
                : properties.SubscriptionId;

            // Construct Azure Resource Management client using ClientSecretCredential based on properties inside AkvProperties;
            ArmClient = new ArmClient(new ClientSecretCredential(properties.TenantId, properties.ApplicationId,
                properties.ClientSecret));

            // Get subscription resource defined by resource ID
            Subscription = ArmClient.GetDefaultSubscription();
            Log.LogDebug("Found subscription called \"{SubscriptionDisplayName}\" ({SubscriptionId})",
                Subscription.Data.DisplayName, Subscription.Data.SubscriptionId);
        }

        public AzureAppServicesClient(string subscriptionId, string tenantId = "")
        {
            // Each AzureAppServicesClient represents a single resource group (and therefore subscription).
            // Each AzureAppServicesClient represents a single subscription.

            Log = LogHandler.GetClassLogger<AzureAppServicesClient>();
            Log.LogDebug("Initializing Azure App Services client");

            // Create Azure Resource Management client
            Log.LogDebug("Getting Azure token using DefaultAzureCredential");
            if (string.IsNullOrEmpty(tenantId))
                ArmClient = new ArmClient(new DefaultAzureCredential());
            else
                ArmClient = new ArmClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    { TenantId = tenantId }));

            // Get subscription resource defined by resource ID
            // TODO this should actually be getDefaultSubscription() but that doesn't work for some reason
            Subscription = ArmClient.GetSubscriptions().Get(subscriptionId);
            Log.LogDebug("Found subscription called \"{SubscriptionName}\" ({SubscriptionId})",
                Subscription.Data.DisplayName, Subscription.Data.SubscriptionId);
        }

        private ArmClient ArmClient { get; }
        private SubscriptionResource Subscription { get; }
        private ILogger Log { get; }

        private string FilterWildcard(string dnsName)
        {
            return dnsName.Contains("*") ? dnsName.Replace("*", "") : dnsName;
        }

        public IEnumerable<WebSiteResource> GetSiteResourceFromHostname(string hostname)
        {
            // Use LINQ syntax to find the site with matching hostname.
            // Search across every resource group in the subscription.
            return (
                from resourceGroup in Subscription.GetResourceGroups().GetAll()
                from site in resourceGroup.GetWebSites().GetAll()
                from hostnameBinding in site.Data.HostNames
                where hostnameBinding.Contains(hostname)
                select site).ToList();
        }

        #region Removal

        public void RemoveCertificate(string thumbprint)
        {
            foreach (AppCertificateResource certificateResource in GetCertificateResourceByThumbprint(thumbprint))
                certificateResource?.Delete(WaitUntil.Completed);
        }

        #endregion

        #region Download

        public IEnumerable<AppCertificateResource> GetCertificateResourceByThumbprint(string thumbprint)
        {
            // Query across all resource groups in the subscription to find the certificate with the matching name
            // Note that the same certificate could be deployed across multiple resource groups, so there could be
            // more than 1 AppCertificateResource returned.
            return (
                from resourceGroup in Subscription.GetResourceGroups().GetAll()
                from cert in resourceGroup.GetAppCertificates().GetAll()
                // AppCertificateResource wraps thumbprint in '"' so we need to remove them before comparing
                where $"{cert.Data.Thumbprint}".Replace("\"", "") == thumbprint
                select cert
            ).ToList();
        }

        #endregion

        #region Import

        public IEnumerable<AppCertificateResource> ImportCertificateFromAzureKeyVault(
            ResourceIdentifier keyVaultResourceId,
            KeyVaultCertificateWithPolicy akvCertificateName)
        {
            return from dnsName in akvCertificateName.Policy.SubjectAlternativeNames.DnsNames
                select GetSiteResourceFromHostname(dnsName)
                into sites // Get the site resource for each DNS name
                from site in sites
                where site != null // Filter out any sites that don't match
                select ImportCertificateFromAzureKeyVault(site.Id, keyVaultResourceId,
                    akvCertificateName.Name); // Import the certificate into the site
        }

        public AppCertificateResource ImportCertificateFromAzureKeyVault(ResourceIdentifier appServiceResourceId,
            ResourceIdentifier keyVaultResourceId, string keyVaultSecretName)
        {
            // Get Azure Web Site resource using resource ID to get location and app service plan ID
            WebSiteResource site = ArmClient.GetWebSiteResource(appServiceResourceId).Get();
            Log.LogDebug("Got WebSiteResource for {Name}", site.Data.Name);

            // Get location from Web Site resource
            AzureLocation location = site.Data.Location;
            ResourceIdentifier appServicePlanId = site.Data.AppServicePlanId;

            Log.LogDebug("Importing certificate with name {Name} from Key Vault {VaultName}", keyVaultSecretName,
                keyVaultResourceId.Name);

            // Get resource group resource
            string resourceGroupId =
                $"/subscriptions/{appServiceResourceId.SubscriptionId}/resourceGroups/{appServiceResourceId.ResourceGroupName}";
            ResourceGroupResource resourceGroup =
                ArmClient.GetResourceGroupResource(new ResourceIdentifier(resourceGroupId)).Get();

            return resourceGroup.GetAppCertificates().CreateOrUpdate(WaitUntil.Completed,
                keyVaultSecretName,
                new AppCertificateData(location)
                {
                    KeyVaultSecretName = keyVaultSecretName,
                    KeyVaultId = keyVaultResourceId,
                    ServerFarmId = appServicePlanId
                }).WaitForCompletion();
        }

        #endregion

        #region Bindings

        public IEnumerable<string> GetHostnameBindings(ResourceIdentifier appServiceResourceId)
        {
            // Get Web Site resource using resource ID
            WebSiteResource site = ArmClient.GetWebSiteResource(appServiceResourceId).Get();
            Log.LogDebug("Found {0} hostname bindings for {1}", site.Data.HostNames.Count, site.Data.Name);

            // Get hostname bindings for Web Site resource
            return site.Data.HostNames.ToList();
        }

        public SiteHostNameBindingResource UpdateCertificateBinding(ResourceIdentifier appServiceResourceId,
            AppCertificateResource certificateResource, HostNameBindingSslState? sslState)
        {
            // Get Azure Web Site resource using resource ID to get location and app service plan ID
            WebSiteResource appServiceResource = ArmClient.GetWebSiteResource(appServiceResourceId).Get();

            SiteHostNameBindingCollection bindings = appServiceResource.GetSiteHostNameBindings();

            // Try to add certificate to each matching hostname
            foreach (string host in appServiceResource.Data.HostNames)
            {
                if (!certificateResource.Data.HostNames.Contains(host)) continue;

                bindings.CreateOrUpdate(WaitUntil.Completed, host, new HostNameBindingData
                {
                    SiteName = appServiceResource.Data.RepositorySiteName,
                    AzureResourceName = appServiceResource.Data.Name,
                    SslState = sslState,
                    Thumbprint = certificateResource.Data.Thumbprint
                });
                Log.LogDebug("Bound certificate with name {Name} to hostname {Hostname}", certificateResource.Data.Name,
                    host);
            }

            return IsCertificateBoundToAppService(appServiceResource, certificateResource);
        }

        public IEnumerable<string> UpdateCertificateBinding(AppCertificateResource certificate)
        {
            // Iterate through all DNS SANS attached to certificate, and try to update bindings for each.
            return (
                from host in certificate.Data.HostNames
                select GetSiteResourceFromHostname(host)
                into sites
                from site in sites
                where site != null
                select UpdateCertificateBinding(site.Id, certificate, HostNameBindingSslState.SniEnabled).Data.Name
            ).ToList();
        }

        public IEnumerable<string> RemoveCertificateBinding(KeyVaultCertificateWithPolicy akvCertificateName)
        {
            string thumb = akvCertificateName.Properties.X509Thumbprint.Aggregate("", (current, b) => current + b.ToString("X2"));;

            // Search for the site that the certificate is bound to across all resource groups in the subscription
            // Then, remove the certificate binding from the site.

            // Returns a list of thumbprints with size according to the number of binding removals.
            return (
                from appCert in GetCertificateResourceByThumbprint(thumb) // Get list of cert resources
                from resourceGroupResource in Subscription.GetResourceGroups().GetAll()
                from webSiteResource in resourceGroupResource.GetWebSites().GetAll()
                from binding in webSiteResource.GetSiteHostNameBindings().GetAll()
                where appCert.Data.HostNames.Any(host => binding.Data.Name.Contains(host))
                select RemoveCertificateBinding(webSiteResource.Id, appCert)
            ).ToList();
        }

        public string RemoveCertificateBinding(ResourceIdentifier appServiceResourceId,
            AppCertificateResource certificateResource)
        {
            WebSiteResource appServiceResource = ArmClient.GetWebSiteResource(appServiceResourceId).Get();
            string removalBindingThumbprint = null;

            foreach (SiteHostNameBindingResource binding in appServiceResource.GetSiteHostNameBindings().GetAll())
            {
                if ($"{binding.Data.Thumbprint}" != $"{certificateResource.Data.Thumbprint}") continue;

                removalBindingThumbprint = $"{binding.Data.Thumbprint}";
                binding.Update(WaitUntil.Completed, new HostNameBindingData
                {
                    SiteName = binding.Data.SiteName,
                    AzureResourceName = binding.Data.Name,
                    SslState = HostNameBindingSslState.Disabled,
                    Thumbprint = null
                });
                Log.LogDebug("Removed certificate binding for {Name}", binding.Data.Name);
            }

            return removalBindingThumbprint;
        }

        public SiteHostNameBindingResource IsCertificateBoundToAppService(WebSiteResource appServiceResource,
            AppCertificateResource cert)
        {
            if (cert == null) return null;

            // Get bindings from Web Site resource
            foreach (SiteHostNameBindingResource binding in appServiceResource.GetSiteHostNameBindings().GetAll())
                if (binding != null && Equals($"{cert.Data.Thumbprint}", $"{binding.Data.Thumbprint}"))
                {
                    Log.LogDebug("Certificate with thumbprint {Thumb} is bound to {Name}", cert.Data.Thumbprint,
                        appServiceResource.Data.Name);
                    return binding;
                }

            Log.LogDebug("Certificate with thumbprint {Thumb} is not bound to {Name}", cert.Data.Thumbprint,
                appServiceResource.Data.Name);
            return null;
        }

        #endregion
    }
}