namespace WhoAndWhat.Infrastructure.Configuration;

public class OAuthSettings
{
    public const string SectionName = "OAuth";

    public GoogleOAuthSettings Google { get; set; } = new();
    public FacebookOAuthSettings Facebook { get; set; } = new();
    public AppleOAuthSettings Apple { get; set; } = new();
    public OAuthCallbackSettings CallbackUrl { get; set; } = new();
}

public class GoogleOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid profile email";
}

public class FacebookOAuthSettings
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "email public_profile";
}

public class AppleOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "name email";
}

public class OAuthCallbackSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Google { get; set; } = "/api/v1/oauth/google/callback";
    public string Facebook { get; set; } = "/api/v1/oauth/facebook/callback";
    public string Apple { get; set; } = "/api/v1/oauth/apple/callback";
}