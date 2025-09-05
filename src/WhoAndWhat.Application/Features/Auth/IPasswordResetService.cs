
namespace WhoAndWhat.Application.Features.Auth;

public interface IPasswordResetService
{
    public Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    public Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
}
