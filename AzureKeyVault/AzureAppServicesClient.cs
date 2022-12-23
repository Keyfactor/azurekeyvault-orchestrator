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
using Microsoft.VisualBasic;
using SiteHostNameBindingResource = Azure.ResourceManager.AppService.SiteHostNameBindingResource;

namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AzureAppServicesClient
    {
        // Environment variables required to authenticate using DefaultAzureCredential:
        // - AZURE_CLIENT_ID → The app ID value.
        // - AZURE_TENANT_ID → The tenant ID value.
        // - AZURE_CLIENT_SECRET → The password/credential generated for the app.

        public AzureAppServicesClient(AkvProperties properties)
        {
            // Each AzureAppServicesClient represents a single resource group (and therefore subscription).

            Log = LogHandler.GetClassLogger<AzureAppServicesClient>();
            Log.LogDebug("Initializing Azure App Services client");

            // Build ResourceGroupId using SubscriptionId, and ResourceGroupName from properties
            string subscriptionId = string.IsNullOrEmpty(properties.SubscriptionId) ? new ResourceIdentifier(properties.StorePath).SubscriptionId : properties.SubscriptionId;
            ResourceGroupId =
                new ResourceIdentifier(
                    $"/subscriptions/{subscriptionId}/resourceGroups/{properties.ResourceGroupName}");

            // Construct Azure Resource Management client using ClientSecretCredential based on properties inside AkvProperties;
            ArmClient = new ArmClient(new ClientSecretCredential(properties.TenantId, properties.ApplicationId, properties.ClientSecret));

            // Get subscription resource defined by resource ID
            Subscription = ArmClient.GetSubscriptions().Get(ResourceGroupId.SubscriptionId);
            Log.LogDebug("Found subscription called \"{SubscriptionDisplayName}\" ({SubscriptionId})",
                Subscription.Data.DisplayName, Subscription.Data.SubscriptionId);

            // Get resource group resource for later use
            ResourceGroup = Subscription.GetResourceGroup(ResourceGroupId.ResourceGroupName);
            Log.LogDebug("Got resource group resource called \"{ResourceGroupName}\" ({ResourceGroupId})",
                ResourceGroup.Data.Name, ResourceGroup.Data.Id);
        }

        public AzureAppServicesClient(ResourceIdentifier resourceGroupId, string tenantId = "")
        {
            // Each AzureAppServicesClient represents a single resource group (and therefore subscription).

            Log = LogHandler.GetClassLogger<AzureAppServicesClient>();
            Log.LogDebug("Initializing Azure App Services client");

            // Create resource identifier object from resourceId
            ResourceGroupId = resourceGroupId;

            // Create Azure Resource Management client
            Log.LogDebug("Getting Azure token using DefaultAzureCredential");
            if (string.IsNullOrEmpty(tenantId))
                ArmClient = new ArmClient(new DefaultAzureCredential());
            else
                ArmClient = new ArmClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    { TenantId = tenantId }));

            // Get subscription resource defined by resource ID
            Subscription = ArmClient.GetSubscriptions().Get(ResourceGroupId.SubscriptionId);
            Log.LogDebug("Found subscription called \"{SubscriptionName}\" ({SubscriptionId})",
                Subscription.Data.DisplayName, Subscription.Data.SubscriptionId);

            // Get resource group resource for later use
            ResourceGroup = Subscription.GetResourceGroup(ResourceGroupId.ResourceGroupName);
            Log.LogDebug("Got resource group resource called \"{ResourceGroupName}\" ({ResourceGroupId})",
                ResourceGroup.Data.Name, ResourceGroup.Data.Id);
        }

        private ArmClient ArmClient { get; }
        private SubscriptionResource Subscription { get; }
        private ResourceGroupResource ResourceGroup { get; }
        private ResourceIdentifier ResourceGroupId { get; }
        private ILogger Log { get; }

        #region Import

        public AppCertificateResource ImportCertificateFromAkv(ResourceIdentifier appServiceResourceId,
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
            
            return ResourceGroup.GetAppCertificates().CreateOrUpdate(WaitUntil.Completed,
                keyVaultSecretName,
                new AppCertificateData(location)
                {
                    KeyVaultSecretName = keyVaultSecretName,
                    KeyVaultId = keyVaultResourceId,
                    ServerFarmId = appServicePlanId
                }).WaitForCompletion();
        }

        #endregion

        #region Removal

        public void RemoveCertificate(string name)
        {
            AppCertificateResource cert = GetCertificateResourceByName(name);
            if (cert == null) return;

            cert.Delete(WaitUntil.Completed);
        }

        #endregion

        #region Download

        public AppCertificateResource GetCertificateResourceByName(string name)
        {
            return ResourceGroup.GetAppCertificates().GetAll().FirstOrDefault(cert => Equals(cert.Data.Name, name));
        }

        #endregion

        #region Bindings

        public List<string> GetHostnameBindings(ResourceIdentifier appServiceResourceId)
        {
            // Get Web Site resource using resource ID
            WebSiteResource site = ArmClient.GetWebSiteResource(appServiceResourceId).Get();
            Log.LogDebug("Found {0} hostname bindings for {1}", site.Data.HostNames.Count, site.Data.Name);
            
            // Get hostname bindings for Web Site resource
            return site.Data.HostNames.ToList();
        }

        public WebSiteResource GetSiteResourceFromHostname(string hostname)
        {
            // Use LINQ syntax to find the site with matching hostname
            return (from siteResource in ResourceGroup.GetWebSites().GetAll()
                let bindings = siteResource.GetSiteHostNameBindings()
                from binding in bindings.GetAll()
                where binding.Data.Name.Contains(hostname)
                select siteResource).FirstOrDefault();
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
                Log.LogDebug("Bound certificate with name {Name} to hostname {Hostname}", certificateResource.Data.Name, host);
            }

            return IsCertificateBoundToAppService(appServiceResource, certificateResource);
        }

        public List<string> UpdateCertificateBinding(ResourceIdentifier keyVaultResourceId,
            KeyVaultCertificateWithPolicy akvCertificateName)
        {
            // Create a list of SiteHostNameBindingResource to return
            List<string> bindings = new List<string>();

            // Iterate through all DNS SANS attached to certificate, and try to update bindings for each.
            foreach (string dnsName in akvCertificateName.Policy.SubjectAlternativeNames.DnsNames)
            {
                WebSiteResource site = GetSiteResourceFromHostname(dnsName); // Null is returned if no site is found.
                if (site == null) continue;

                // If it matches, import the AKV certificate into the app service
                AppCertificateResource appCert = ImportCertificateFromAkv(site.Id, keyVaultResourceId, akvCertificateName.Name);

                // Finally, update the hostname binding to use the App Service certificate
                bindings.Add(UpdateCertificateBinding(site.Id, appCert, HostNameBindingSslState.SniEnabled).Data.Name);
            }

            return bindings;
        }

        public void RemoveCertificateBinding(KeyVaultCertificateWithPolicy akvCertificateName)
        {
            // Get the corresponding App Service certificate resource
            AppCertificateResource appCert = GetCertificateResourceByName(akvCertificateName.Name);
            if (appCert == null) return;

            // Iterate through each web site and remove the certificate binding if it matches the AKV certificate
            foreach (WebSiteResource site in ResourceGroup.GetWebSites().GetAll())
            {
                foreach (string host in site.Data.HostNames)
                {
                    if (appCert.Data.HostNames.Contains(host))
                    {
                        RemoveCertificateBinding(site.Id, appCert);
                    }
                }
            }
        }

        public void RemoveCertificateBinding(ResourceIdentifier appServiceResourceId,
            AppCertificateResource certificateResource)
        {
            WebSiteResource appServiceResource = ArmClient.GetWebSiteResource(appServiceResourceId).Get();

            foreach (SiteHostNameBindingResource binding in appServiceResource.GetSiteHostNameBindings().GetAll())
            {
                if ($"{binding.Data.Thumbprint}" != $"{certificateResource.Data.Thumbprint}") continue;

                binding.Update(WaitUntil.Completed, new HostNameBindingData()
                {
                    SiteName = binding.Data.SiteName,
                    AzureResourceName = binding.Data.Name,
                    SslState = HostNameBindingSslState.Disabled,
                    Thumbprint = null
                });
                Log.LogDebug("Removed certificate binding for {Name}", binding.Data.Name);
            }
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