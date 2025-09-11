using MediatR;
using WhoAndWhat.Application.Common;
using WhoAndWhat.Application.DTOs.Contacts;

namespace WhoAndWhat.Application.Features.Contacts.Queries.GetContacts;

public record GetContactsQuery(
    Guid UserId,
    string? Search = null,
    List<int>? RelationshipTypes = null,
    bool IncludeDeleted = false,
    string? SortBy = "Name",
    bool SortDescending = false,
    int PageSize = 20,
    int PageNumber = 1
) : IRequest<Result<ContactSearchResult>>;