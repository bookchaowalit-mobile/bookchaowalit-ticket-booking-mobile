using TicketBooking.Core;
using Xunit;

namespace TicketBooking.Core.Tests;

public class Pass3Tests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static SeatId S(string s) => SeatId.Parse(s);

    private static BookingService Service(TimeSpan? holdFor = null) =>
        new(new SeatMap(5, 6), new PriceList(200m, 300m, 500m, 15m), holdFor ?? TimeSpan.FromMinutes(10));

    [Fact]
    public void QuoteRejectsDuplicateSeatsInsteadOfChargingTwice()
    {
        // HoldSeats already rejects duplicates; the quote shown before holding must agree.
        Assert.Throws<ArgumentException>(() => Service().Price(new[] { S("A1"), S("A1") }));
    }

    [Theory]
    [InlineData("EARLYBIRD​")]
    [InlineData("﻿earlybird")]
    [InlineData(" early⁠bird ")]
    public void PromoCodeIgnoresZeroWidthCharacters(string promo)
    {
        var q = Service().Price(new[] { S("A1") }, promo);
        Assert.Equal(40m, q.Discount);
    }

    [Fact]
    public void ConfirmAcceptsReferenceTypedInLowerCaseOrPadded()
    {
        var svc = Service();
        var hold = svc.HoldSeats(new[] { S("A1"), S("A2") }, Now);
        svc.Confirm(" " + hold.Reference.ToLowerInvariant() + " ", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => svc.Confirm(hold.Reference, Now.AddMinutes(1)));
    }

    [Fact]
    public void NonPositiveHoldDurationIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Service(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Service(TimeSpan.FromMinutes(-5)));
    }
}
