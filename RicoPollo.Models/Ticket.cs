namespace RicoPollo.Models
{
    public enum ItemStatuses { Pending, Complete, Failed};
    public record Ticket(
        Guid TicketId, 
        int TicketOrder, 
        ItemStatuses ChickenStatus, 
        int ChickenAttempt,
        ItemStatuses FriesStatus,
        int FriesAttempt,
        ItemStatuses TakeoutStatus,
        int TakeoutAttempt){}
}
