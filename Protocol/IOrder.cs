namespace Protocol;

public interface IOrder
{
    Guid Id { get; }
    string Address { get; set; }
    string Description { get; set; }
}