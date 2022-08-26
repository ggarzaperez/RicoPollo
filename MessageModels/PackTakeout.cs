namespace RicoPollo.Models
{
    public record PackTakeout(Guid TicketId, int TicketOrder, int attempt = 0) { }
}
