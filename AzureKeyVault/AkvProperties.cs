namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AkvProperties
    {
        public string SubscriptionId { get; set; }
        public string ApplicationId { get; set; } // (a.k.a. ClientId)
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string ResourceGroupName { get; set; }
        public string VaultName { get; set; }
        public string StorePath { get; set; }
        public string VaultRegion { get; set; }
        public bool PremiumSKU { get; set; }
        public bool UseAzureManagedIdentity { get; set; }

        internal protected string VaultURL => $"https://{VaultName}.vault.azure.net/";
    }
}
