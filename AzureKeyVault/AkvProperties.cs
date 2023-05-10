namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AkvProperties
    {
        public string SubscriptionId { get; set; }
        public string ClientId { get; set; } // (a.k.a. ClientId)
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string ResourceGroupName { get; set; }
        public string VaultName { get; set; }
        public string StorePath { get; set; }
        public string VaultRegion { get; set; }
        public bool PremiumSKU { get; set; }
        internal protected bool UseAzureManagedIdentity
        {
            get
            {
                return string.IsNullOrEmpty(ClientSecret); //if they don't provide a client secret, assume they are using Azure Managed Identities
            }
        }

        internal protected string VaultURL => $"https://{VaultName}.vault.azure.net/";
    }
}
