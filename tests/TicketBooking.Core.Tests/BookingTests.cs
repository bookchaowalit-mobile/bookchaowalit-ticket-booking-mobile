using TicketBooking.Core;
using Xunit;

namespace TicketBooking.Core.Tests;

public class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static SeatId S(string s) => SeatId.Parse(s);

    private static (SeatMap map, BookingService svc) Setup()
    {
        var map = new SeatMap(5, 6, row => row switch { 'A' => SeatTier.Vip, 'B' or 'C' => SeatTier.Premium, _ => SeatTier.Standard });
        var svc = new BookingService(map, new PriceList(200m, 300m, 500m, 15m), TimeSpan.FromMinutes(10));
        return (map, svc);
    }

    [Fact]
    public void ParsesSeatIds()
    {
        Assert.Equal(new SeatId('C', 7), SeatId.Parse(" c7 "));
        Assert.Equal("C7", new SeatId('C', 7).ToString());
        Assert.Throws<FormatException>(() => SeatId.Parse("7C"));
        Assert.Throws<FormatException>(() => SeatId.Parse("A0"));
    }

    [Fact]
    public void QuotesWithTiersFeesAndPromo()
    {
        var (_, svc) = Setup();
        var q = svc.Price(new[] { S("A1"), S("B1"), S("D1") }, "earlybird");
        Assert.Equal(1000m, q.Subtotal);
        Assert.Equal(200m, q.Discount);
        Assert.Equal(45m, q.Fees);
        Assert.Equal(845m, q.Total);
        Assert.Equal(0m, svc.Price(new[] { S("D1") }, "BOGUS").Discount);
    }

    [Fact]
    public void HoldAndConfirm()
    {
        var (map, svc) = Setup();
        var hold = svc.HoldSeats(new[] { S("D1"), S("D2") }, Now);
        Assert.Equal(6, hold.Reference.Length);
        Assert.Equal(SeatState.Held, map.StateOf(S("D1")));
        Assert.Throws<InvalidOperationException>(() => svc.HoldSeats(new[] { S("D2"), S("D3") }, Now));
        svc.Confirm(hold.Reference, Now.AddMinutes(5));
        Assert.Equal(2, map.Count(SeatState.Booked));
    }

    [Fact]
    public void ExpiredHoldsReleaseSeats()
    {
        var (map, svc) = Setup();
        var hold = svc.HoldSeats(new[] { S("E1"), S("E2") }, Now);
        Assert.Throws<InvalidOperationException>(() => svc.Confirm(hold.Reference, Now.AddMinutes(10)));
        Assert.Equal(SeatState.Available, map.StateOf(S("E1")));
        Assert.Equal(0, map.Count(SeatState.Held));
    }

    [Fact]
    public void RejectsInvalidSelections()
    {
        var (_, svc) = Setup();
        Assert.Throws<ArgumentException>(() => svc.HoldSeats(Array.Empty<SeatId>(), Now));
        Assert.Throws<ArgumentException>(() => svc.HoldSeats(new[] { S("D1"), S("D1") }, Now));
        Assert.Throws<ArgumentException>(() => svc.HoldSeats(Enumerable.Range(1, 9).Select(i => new SeatId('E', (i % 6) + 1)).ToList(), Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => svc.HoldSeats(new[] { S("Z1") }, Now));
    }

    [Fact]
    public void OrphanSeatRule()
    {
        var (map, svc) = Setup();
        // Leaving D1 alone between the wall and D2 is not allowed.
        Assert.Throws<InvalidOperationException>(() => svc.HoldSeats(new[] { S("D2"), S("D3") }, Now));
        Assert.True(map.LeavesOrphanSeat(new[] { S("C1"), S("C3") }));
        Assert.False(map.LeavesOrphanSeat(new[] { S("C1"), S("C2") }));
        svc.HoldSeats(new[] { S("D1"), S("D2"), S("D3") }, Now);
    }

    [Fact]
    public void ReferencesAreUnique()
    {
        var (_, svc) = Setup();
        var a = svc.HoldSeats(new[] { S("A1"), S("A2") }, Now);
        var b = svc.HoldSeats(new[] { S("A3"), S("A4") }, Now);
        Assert.NotEqual(a.Reference, b.Reference);
        Assert.DoesNotContain('0', a.Reference + b.Reference);
    }
}
