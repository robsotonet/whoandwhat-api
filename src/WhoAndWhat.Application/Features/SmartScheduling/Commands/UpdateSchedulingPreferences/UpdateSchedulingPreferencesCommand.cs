using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.SmartScheduling;

namespace WhoAndWhat.Application.Features.SmartScheduling.Commands.UpdateSchedulingPreferences;

public record UpdateSchedulingPreferencesCommand(
    Guid UserId,
    SmartSchedulingPreferences Preferences
) : IRequest<Result<UpdateSchedulingPreferencesResponse>>;