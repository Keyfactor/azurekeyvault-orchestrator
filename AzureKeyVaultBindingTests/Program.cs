using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Certificates;
using Keyfactor.Extensions.Orchestrator.AzureKeyVault;

namespace AzureKeyVaultBindingTests
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Test the Azure Key Vault binding capability
            
            // 1. Set up the Azure Key Vault client and Azure App Service client
            Program p = new Program();
            
            // 2. Get a bound hostname from the app service that isn't the default hostname
            string hostname = p.GetHostname();
            Console.Write("Using hostname " + hostname + "\n");
            
            // 3. Enroll a certificate for the hostname with one SAN
            X509Certificate2 selfSignedCert = p.GetSelfSignedCert(hostname);
            
            // 4. Upload the certificate to the Azure Key Vault
            string certName = "AKVTest" + Guid.NewGuid().ToString().Substring(0, 6);
            KeyVaultCertificateWithPolicy akvCert = p.UploadCertToAkv(selfSignedCert, certName);

            // 5. Bind the certificate to the app service
            p.BindCertificateToAppService(akvCert);
            
            // 6. Verify the certificate is bound to the app service
            p.VerifyCertificateBinding(certName);
            
            // 7. Unbind the certificate from the app service
            p.DeleteCertificateBinding(certName);

            // 8. Delete the certificate from the Azure Key Vault
            p.DeleteCertFromAkv(certName);
        }

        public Program()
        {
            Console.Write("Configuring clients\n");

            AppServiceClient = new AzureAppServicesClient(_resourceGroupId);
            Console.Write("Created AppServiceClient\n");
            
            AkvProperties vaultProperties = new AkvProperties()
            {
                TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty,
                ResourceGroupName = _resourceGroupId.ResourceGroupName,
                VaultName = Environment.GetEnvironmentVariable("AKV_NAME") ?? string.Empty,
                ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty,
                AutoUpdateAppServiceBindings = true,
                StorePath = _keyVaultResourceId,
            };
            KeyVaultClient = new AzureClient(vaultProperties);
            Console.Write("Created KeyVaultClient\n");
        }

        private readonly ResourceIdentifier _resourceGroupId = new ResourceIdentifier(Environment.GetEnvironmentVariable("RESOURCE_GROUP_ID") ?? string.Empty);
        private readonly ResourceIdentifier _appServiceResourceId = new ResourceIdentifier(Environment.GetEnvironmentVariable("APP_SERVICE_ID") ?? string.Empty);
        private readonly ResourceIdentifier _keyVaultResourceId = new ResourceIdentifier(Environment.GetEnvironmentVariable("AKV_ID") ?? string.Empty);
        private AzureAppServicesClient AppServiceClient { get; }
        private AzureClient KeyVaultClient { get; }

        public string GetHostname()
        {
            return AppServiceClient.GetHostnameBindings(_appServiceResourceId).First(host => !host.Contains("azurewebsites.net"));
        }

        public X509Certificate2 GetSelfSignedCert(string hostname)
        {
            RSA rsa = RSA.Create(2048);
            CertificateRequest req = new CertificateRequest($"CN={hostname}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            
            SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
            subjectAlternativeNameBuilder.AddDnsName(hostname);
            req.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
            req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));        
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("2.5.29.32.0"), new Oid("1.3.6.1.5.5.7.3.1") }, false));
            
            X509Certificate2 selfSignedCert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
            Console.Write($"Created self-signed certificate for {hostname} with thumbprint {selfSignedCert.Thumbprint}\n");
            return selfSignedCert;
        }
        
        public KeyVaultCertificateWithPolicy UploadCertToAkv(X509Certificate2 cert, string certName)
        {
            string password = Guid.NewGuid().ToString().Substring(0, 10);
            // Export cert to PFX and base64 encode it
            string pfxBytes = Convert.ToBase64String(cert.Export(X509ContentType.Pfx, password));
            Task<KeyVaultCertificateWithPolicy> createTask = KeyVaultClient.ImportCertificateAsync(certName, pfxBytes, password);
            createTask.Wait();
            KeyVaultCertificateWithPolicy akvCert = createTask.Result;
            Console.Write("Uploaded certificate to Azure Key Vault\n");
            
            AppServiceClient.RemoveCertificateBinding(akvCert);
            return akvCert;
        }

        public void BindCertificateToAppService(KeyVaultCertificateWithPolicy cert)
        {
            string hostname = GetHostname();
            X509Certificate2 ssCert = GetSelfSignedCert(hostname);
            
            WebSiteResource site = AppServiceClient.GetSiteResourceFromHostname(hostname);
            
            ArmClient armClient = new ArmClient(new DefaultAzureCredential());
            SubscriptionResource subscription = armClient.GetSubscriptions().Get(_resourceGroupId.SubscriptionId);
            ResourceGroupResource resourceGroup = subscription.GetResourceGroups().Get(_resourceGroupId.ResourceGroupName);

            try
            {
                resourceGroup.GetAppCertificates().CreateOrUpdate(WaitUntil.Completed, cert.Name, new AppCertificateData(site.Data.Location)
                {
                    Password = "FooBar",
                    PfxBlob = ssCert.Export(X509ContentType.Pfx, "FooBar"),
                    ServerFarmId = site.Data.AppServicePlanId,
                }).WaitForCompletion();
            } catch (System.ArgumentException e)
            {
                // Do nothing because it's gonna fail
                Console.Write("Uploaded certificate\n");
            }

            AppCertificateResource appCert = AppServiceClient.GetCertificateResourceByName(cert.Name);

            AppServiceClient.UpdateCertificateBinding(_appServiceResourceId, appCert,
                HostNameBindingSslState.SniEnabled);
            
            AppServiceClient.RemoveCertificateBinding(_appServiceResourceId, appCert);

            AppServiceClient.RemoveCertificate(cert.Name);

            // This is the only thing that needs to be called
            //AppServiceClient.UpdateCertificateBinding(_keyVaultResourceId, cert);
        }

        public void VerifyCertificateBinding(string name)
        {
            WebSiteResource site = AppServiceClient.GetSiteResourceFromHostname(GetHostname());
            AppCertificateResource certResource = AppServiceClient.GetCertificateResourceByName(name);
            SiteHostNameBindingResource bind = AppServiceClient.IsCertificateBoundToAppService(site, certResource);
            Console.Write("Certificate is {0}bound to app service\n", bind == null ? "not " : "");
        }
        
        public void DeleteCertificateBinding(string name)
        {
            KeyVaultCertificateWithPolicy cert = KeyVaultClient.GetCertificate(name);
            AppServiceClient.RemoveCertificateBinding(cert);
            AppServiceClient.RemoveCertificate(cert.Name);
            Console.Write("Deleted certificate binding\n");
        }
        
        public void DeleteCertFromAkv(string certName)
        {
            Task deleteTask = KeyVaultClient.DeleteCertificateAsync(certName);
            deleteTask.Wait();
            Console.Write("Deleted certificate from Azure Key Vault\n");
        }

    }
}