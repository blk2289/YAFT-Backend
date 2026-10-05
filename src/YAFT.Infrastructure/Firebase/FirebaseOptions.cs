namespace YAFT.Infrastructure.Firebase;

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";

    public string ProjectId { get; set; } = "";

    /// Percorso del service account JSON. Lascia vuoto per usare le Application Default Credentials
    /// (variabile GOOGLE_APPLICATION_CREDENTIALS o identità del servizio su Google Cloud).
    public string? CredentialPath { get; set; }
}
