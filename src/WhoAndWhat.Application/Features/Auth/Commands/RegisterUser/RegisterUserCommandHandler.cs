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
    private readonly IUserDomainService _userDomainService;
    private readonly IUserRepository _userRepository;
    private readonly IAccountVerificationService _accountVerificationService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IUserDomainService userDomainService,
        IUserRepository userRepository,
        IAccountVerificationService accountVerificationService,
        IJwtTokenService jwtTokenService,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _userDomainService = userDomainService;
        _userRepository = userRepository;
        _accountVerificationService = accountVerificationService;
        _jwtTokenService = jwtTokenService;
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

            // Check if user already exists
            var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
                return Result<RegisterResponse>.Failure("User with this email already exists");
            }

            // Check if username is taken
            var existingUsername = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
            if (existingUsername != null)
            {
                _logger.LogWarning("Registration attempt with existing username: {Username}", request.Username);
                return Result<RegisterResponse>.Failure("Username is already taken");
            }

            // Parse and validate language
            if (!Enum.TryParse<Language>(request.PreferredLanguage, true, out var language))
            {
                _logger.LogWarning("Invalid language provided during registration: {Language}", request.PreferredLanguage);
                return Result<RegisterResponse>.Failure("Invalid language. Supported languages are: en, es");
            }

            // Create user using domain service
            var user = _userDomainService.CreateUser(
                request.Email,
                request.Username,
                request.Password,
                language);

            // Save user to database
            await _userRepository.AddAsync(user, cancellationToken);
            var saveResult = await _userRepository.SaveChangesAsync(cancellationToken);
            
            if (saveResult <= 0)
            {
                _logger.LogError("Failed to save user to database: {Email}", request.Email);
                return Result<RegisterResponse>.Failure("Failed to save user to database");
            }

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

            // TODO: Send verification email (implement email service)
            if (requiresEmailVerification)
            {
                _logger.LogInformation("Email verification required for user: {UserId}, Token: {Token}", user.Id, verificationToken);
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