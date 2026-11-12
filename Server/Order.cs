using System.ComponentModel;
using System.Net;
using Protocol;
using Protocol.DI;

namespace Server;

public class Order : IOrder
{
    public Guid Id { get; }
    public string Address { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAd { get; set; }
    public Guid CourierId  { get; set; } 
    public OrderStatus Status { get; set; }
    public int Attempts { get; set; }
    public long LastSentAt { get; set; } //когда последний раз отправляли, можно DateTime мб
    public long AckedAt { get; set; } //когда курьер подтвердил получение, нужно ли?
    public HashSet<Guid> RejectedBy { get; set; } //кто отказался, кому не предлагать
    
    
    
    private Order(string description, string address)
    {
        Id = Guid.NewGuid();
        Address = address;
        Description = description;
        CreatedAd = DateTime.Now;
        Status = OrderStatus.New;
    }

    public static Order Create(string description, string address)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentNullException(nameof(description));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentNullException(nameof(address));
        }
        
        return new(description, address);
    }
}