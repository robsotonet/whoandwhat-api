namespace WhoAndWhat.Application.DTOs.Authentication;

public class TokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}