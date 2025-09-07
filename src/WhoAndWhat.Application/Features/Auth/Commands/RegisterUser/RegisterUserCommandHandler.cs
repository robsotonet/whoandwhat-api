using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Authentication;
using WhoAndWhat.Application.Interfaces;
using WhoAndWhat.Domain.Services;
using WhoAndWhat.Domain.ValueObjects;

namespace WhoAndWhat.Application.Features.Auth.Commands.RegisterUser;

/// <summary>
/// Handler for user registration command
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<RegisterResponse>>
{
    private readonly IUserService _userService;
    private readonly IAccountVerificationService _accountVerificationService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IUserService userService,
        IAccountVerificationService accountVerificationService,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userService = userService;
        _accountVerificationService = accountVerificationService;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<RegisterResponse>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if terms are accepted
            if (!request.AcceptTerms)
            {
                _logger.LogWarning("Registration attempt without accepting terms for email: {Email}", request.Email);
                return Result<RegisterResponse>.Failure("You must accept the terms and conditions to register");
            }

            // Parse and validate language
            if (!Enum.TryParse<Language>(request.PreferredLanguage, true, out var language))
            {
                _logger.LogWarning("Invalid language provided during registration: {Language}", request.PreferredLanguage);
                return Result<RegisterResponse>.Failure("Invalid language. Supported languages are: en, es");
            }

            // Use UserService for registration logic
            var registrationResult = await _userService.RegisterUserAsync(
                request.Email,
                request.Username,
                request.Password,
                language,
                cancellationToken);

            if (!registrationResult.IsSuccess)
            {
                return Result<RegisterResponse>.Failure(registrationResult.Error);
            }

            var user = registrationResult.Value;

            // Generate email verification token
            var verificationToken = await _accountVerificationService.GenerateVerificationTokenAsync(user.Id, cancellationToken);
            bool requiresEmailVerification = !string.IsNullOrEmpty(verificationToken);

            if (!requiresEmailVerification)
            {
                _logger.LogWarning("Failed to generate email verification token for user: {UserId}", user.Id);
            }

            _logger.LogInformation("User registered successfully: {UserId}, Email: {Email}", user.Id, user.Email);

            // Prepare response
            var response = new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username,
                PreferredLanguage = user.PreferredLanguage.ToString(),
                RequiresEmailVerification = requiresEmailVerification,
                Message = "User registered successfully. Please check your email for verification instructions."
            };

            // Send verification email if token was generated
            if (requiresEmailVerification && !string.IsNullOrEmpty(verificationToken))
            {
                var emailSent = await _emailService.SendEmailVerificationAsync(
                    user.Email, 
                    user.Username, 
                    verificationToken, 
                    user.Id, 
                    cancellationToken);

                if (emailSent)
                {
                    _logger.LogInformation("Email verification sent successfully for user: {UserId}", user.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to send verification email for user: {UserId}", user.Id);
                }
            }

            return Result<RegisterResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error during user registration for email: {Email}", request.Email);
            return Result<RegisterResponse>.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during user registration for email: {Email}", request.Email);
            return Result<RegisterResponse>.Failure("An error occurred during registration. Please try again.");
        }
    }
}