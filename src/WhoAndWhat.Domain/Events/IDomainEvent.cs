using MediatR;

namespace WhoAndWhat.Domain.Events;

public interface IDomainEvent : INotification
{
    public DateTime DateOccurred { get; }
}
