namespace Notification.Providers.Push.Firebase;

public class FirebaseOptions
{
    public const string SectionName = "Providers:Firebase";

    /// <summary>
    /// Path to the Firebase service-account JSON credential file.
    /// Can be overridden per-tenant via TenantConfig.ProviderSettings["Firebase:CredentialFile"].
    /// </summary>
    public string CredentialFile { get; init; } = string.Empty;

    /// <summary>Firebase project ID (used if not embedded in the credential file).</summary>
    public string ProjectId { get; init; } = string.Empty;
}
