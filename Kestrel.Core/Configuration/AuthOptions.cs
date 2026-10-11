namespace Kestrel.Core.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Authentication";

    public GoogleAuthOptions Google { get; set; } = new();
    public string? ApiKey { get; set; }
    public string[] CorsOrigins { get; set; } = ["http://localhost:4200"];
}

public sealed class GoogleAuthOptions
{
    public string? ClientId { get; set; }
}
