using TrainBooking.Domain.Common.Entities;
using TrainBooking.Domain.Common.Guards;
using TrainBooking.Domain.Trains.DomainEvents;

namespace TrainBooking.Domain.Trains;

public class Train : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;

    private Train() { }

    private Train(Guid id, string name) : base(id)
    {
        Name = name;
        AddDomainEvent(new TrainCreatedDomainEvent(id, name));
    }

    public static Train Create(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.StringTooLong(name, 200);

        return new Train(Guid.CreateVersion7(), name);
    }
}
