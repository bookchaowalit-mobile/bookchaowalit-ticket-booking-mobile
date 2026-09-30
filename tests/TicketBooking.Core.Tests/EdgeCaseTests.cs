using TicketBooking.Core;
using Xunit;

namespace TicketBooking.Core.Tests;

public class EdgeCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static SeatId S(string s) => SeatId.Parse(s);

    private static (SeatMap map, BookingService svc) Setup(int rows = 5, int seats = 6)
    {
        var map = new SeatMap(rows, seats);
        var svc = new BookingService(map, new PriceList(200m, 300m, 500m, 15m), TimeSpan.FromMinutes(10));
        return (map, svc);
    }

    [Theory]
    [InlineData("A+1")]
    [InlineData("A 1")]
    [InlineData("A-1")]
    [InlineData("AA1")]
    [InlineData("")]
    [InlineData("1")]
    public void SeatIdParseRejectsNonDigitNumbers(string text) =>
        Assert.Throws<FormatException>(() => SeatId.Parse(text));

    [Fact]
    public void SeatIdParseAcceptsMultiDigitNumbers() =>
        Assert.Equal(new SeatId('Z', 120), SeatId.Parse("z120"));

    [Fact]
    public void SeatMapValidatesDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeatMap(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeatMap(27, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeatMap(3, 0));
        var map = new SeatMap(26, 1);
        Assert.Equal(26, map.Count(SeatState.Available));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.StateOf(S("A2")));
    }

    [Fact]
    public void PreExistingOrphanDoesNotBlockUnrelatedSelectionInTheSameRow()
    {
        var (map, svc) = Setup();
        svc.HoldSeats(new[] { S("E3") }, Now);                          // E1E2 | E3 | E4E5E6: no orphan
        svc.HoldSeats(new[] { S("E1"), S("E2") }, Now.AddMinutes(1));
        svc.HoldSeats(new[] { S("E4") }, Now.AddMinutes(1));            // E5E6 pair left: ok
        svc.ReleaseExpired(Now.AddMinutes(10));                          // E3's hold expires first
        Assert.Equal(SeatState.Available, map.StateOf(S("E3")));         // E3 is now an existing orphan

        // E5+E6 creates no new orphan, so it is allowed despite E3.
        Assert.False(map.LeavesOrphanSeat(new[] { S("E5"), S("E6") }));
        svc.HoldSeats(new[] { S("E5"), S("E6") }, Now.AddMinutes(10));
        Assert.Equal(4 * 6 + 1, map.Count(SeatState.Available));          // rows A-D free, only E3 left in E
    }

    [Fact]
    public void NewOrphansAreStillRejected()
    {
        var (map, svc) = Setup();
        Assert.True(map.LeavesOrphanSeat(new[] { S("A5") }));            // strands A6 at the wall
        Assert.True(map.LeavesOrphanSeat(new[] { S("A1"), S("A3") }));   // strands A2 between them
        Assert.False(map.LeavesOrphanSeat(new[] { S("A5"), S("A6") }));
        Assert.False(map.LeavesOrphanSeat(new[] { S("A6") }));
        Assert.Throws<InvalidOperationException>(() => svc.HoldSeats(new[] { S("A5") }, Now));
    }

    [Fact]
    public void HoldReferencesAreUniqueAcrossManyHolds()
    {
        var (_, svc) = Setup(1, 2);
        var refs = new HashSet<string>();
        var t = Now;
        for (var i = 0; i < 5000; i++)
        {
            var hold = svc.HoldSeats(new[] { S("A1"), S("A2") }, t);
            Assert.Matches("^[2-9A-HJ-NP-Z]{6}$", hold.Reference);
            Assert.True(refs.Add(hold.Reference));
            t = hold.ExpiresAt; // next HoldSeats releases the expired hold first
        }
    }

    [Fact]
    public void FeesAreNeverDiscountedAndPromoIsTrimmed()
    {
        var (_, svc) = Setup();
        var q = svc.Price(new[] { S("A1") }, "  student ");
        Assert.Equal(20m, q.Discount);
        Assert.Equal(15m, q.Fees);
        Assert.Equal(195m, q.Total);
        Assert.Equal(0m, svc.Price(Array.Empty<SeatId>()).Total);
    }

    [Fact]
    public void ConfirmIsSingleUse()
    {
        var (_, svc) = Setup();
        var hold = svc.HoldSeats(new[] { S("A1"), S("A2") }, Now);
        svc.Confirm(hold.Reference, Now);
        Assert.Throws<InvalidOperationException>(() => svc.Confirm(hold.Reference, Now));
        Assert.Throws<InvalidOperationException>(() => svc.Confirm("NOPE22", Now));
    }
}
