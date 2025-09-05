
using WhoAndWhat.Application.Interfaces;

namespace WhoAndWhat.Application.Features.Auth;

public class AccountVerificationService : IAccountVerificationService
{
    private readonly IUserRepository _userRepository;

    public AccountVerificationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<string?> GenerateVerificationTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsEmailVerified)
        {
            return null;
        }

        var verificationToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.SetVerificationToken(verificationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return verificationToken;
    }

    public async Task<bool> VerifyAccountAsync(string token, CancellationToken cancellationToken = default)
    {
        var user = (await _userRepository.FindAsync(u => u.VerificationToken == token, cancellationToken)).FirstOrDefault();

        if (user is null)
        {
            return false;
        }

        user.VerifyEmail();
        user.ClearVerificationToken();
        await _userRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
