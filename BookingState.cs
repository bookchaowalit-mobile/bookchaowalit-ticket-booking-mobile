using TicketBooking.Core;

namespace TicketBooking;

/// <summary>
/// Demo show held in memory. A backend API must become the source of truth
/// for availability before real sales (see docs/UPGRADE-PLAN.md).
/// </summary>
public static class BookingState
{
    public static SeatMap Map { get; } = new(8, 10, row => row switch
    {
        'A' or 'B' => SeatTier.Vip,
        'C' or 'D' or 'E' => SeatTier.Premium,
        _ => SeatTier.Standard,
    });

    public static PriceList Prices { get; } = new(Standard: 250m, Premium: 400m, Vip: 650m, FeePerTicket: 20m);

    public static BookingService Service { get; } = new(Map, Prices, TimeSpan.FromMinutes(10));
}
