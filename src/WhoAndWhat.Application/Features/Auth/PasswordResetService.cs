
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;

namespace WhoAndWhat.Application.Features.Auth;

public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserDomainService _userDomainService;

    public PasswordResetService(IUserRepository userRepository, IUserDomainService userDomainService)
    {
        _userRepository = userRepository;
        _userDomainService = userDomainService;
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.ResetToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        user.ResetTokenExpires = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour
        await _userRepository.SaveChangesAsync(cancellationToken);

        return user.ResetToken;
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = (await _userRepository.FindAsync(u => u.ResetToken == token && u.ResetTokenExpires > DateTime.UtcNow, cancellationToken)).FirstOrDefault();

        if (user is null)
        {
            return false;
        }

        var (passwordHash, salt) = _userDomainService.CreatePasswordHash(newPassword);
        user.PasswordHash = passwordHash;
        user.Salt = salt;
        user.ResetToken = null;
        user.ResetTokenExpires = null;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
