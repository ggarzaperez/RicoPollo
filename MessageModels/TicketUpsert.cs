namespace RicoPollo.Models
{
    public enum ItemTypes { Ticket, Chicken, Fries, TakeoutBag};
    public enum ItemOperations { Create, MarkCompleted,  MarkIncompleted};

    public record TicketUpsert(Guid TicketId, int TicketOrder, ItemTypes ItemType, ItemOperations ItemOperation){}
}
