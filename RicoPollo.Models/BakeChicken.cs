namespace RicoPollo.Models
{
    public record BakeChicken(Guid TicketId, int TicketOrder, int attempt=0) { }
}