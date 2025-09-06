
namespace WhoAndWhat.Application.Features.Auth;

public interface IAccountVerificationService
{
    public Task<string?> GenerateVerificationTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    public Task<bool> VerifyAccountAsync(string token, CancellationToken cancellationToken = default);
}
