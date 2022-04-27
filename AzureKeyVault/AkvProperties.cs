namespace Keyfactor.Extensions.Orchestrator.AzureKeyVault
{
    public class AkvProperties
    {
        public string SubscriptionId { get; set; }
        public string TenantId { get; set; }
        public string ResourceGroupName { get; set; }
        public string VaultName { get; set; }
        public string StorePath { get; set; }
        internal protected string VaultURL => $"https://{VaultName}.vault.azure.net/";
        public string ApplicationId { get; set; }
        public string ObjectId { get; set; }
        public string ClientSecret { get; set; }
    }
}
