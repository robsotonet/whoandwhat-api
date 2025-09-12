using MediatR;
using WhoAndWhat.Domain.Events;

namespace WhoAndWhat.Infrastructure.Services;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;

    public DomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Dispatch(DomainEvent domainEvent)
    {
        await _mediator.Publish(domainEvent);
    }
}
