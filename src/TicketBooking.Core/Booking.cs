using System.Globalization;

namespace TicketBooking.Core;

public enum SeatTier { Standard, Premium, Vip }

public enum SeatState { Available, Held, Booked, Blocked }

/// <summary>Seat id like "C7": row letter + 1-based number.</summary>
public readonly record struct SeatId(char Row, int Number)
{
    public override string ToString() => $"{Row}{Number}";

    public static SeatId Parse(string text)
    {
        var t = text.Trim().ToUpperInvariant();
        // Digits only after the row letter: rejects "A+1", "A 1", "A-1".
        if (t.Length < 2 || t[0] < 'A' || t[0] > 'Z' || !int.TryParse(t.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
            throw new FormatException($"invalid seat id '{text}'");
        return new SeatId(t[0], n);
    }
}

/// <summary>Rectangular seat map for one show.</summary>
public sealed class SeatMap
{
    private readonly SeatState[,] _state;
    private readonly Func<char, SeatTier> _tierOfRow;

    public int Rows { get; }
    public int SeatsPerRow { get; }

    public SeatMap(int rows, int seatsPerRow, Func<char, SeatTier>? tierOfRow = null)
    {
        if (rows is < 1 or > 26) throw new ArgumentOutOfRangeException(nameof(rows), "1..26 rows");
        if (seatsPerRow < 1) throw new ArgumentOutOfRangeException(nameof(seatsPerRow));
        Rows = rows;
        SeatsPerRow = seatsPerRow;
        _state = new SeatState[rows, seatsPerRow];
        _tierOfRow = tierOfRow ?? (_ => SeatTier.Standard);
    }

    private (int r, int c) Index(SeatId s)
    {
        var r = s.Row - 'A';
        var c = s.Number - 1;
        if (r < 0 || r >= Rows || c < 0 || c >= SeatsPerRow) throw new ArgumentOutOfRangeException(nameof(s), $"seat {s} is not on the map");
        return (r, c);
    }

    public SeatState StateOf(SeatId s) { var (r, c) = Index(s); return _state[r, c]; }
    public SeatTier TierOf(SeatId s) { Index(s); return _tierOfRow(s.Row); }
    internal void Set(SeatId s, SeatState st) { var (r, c) = Index(s); _state[r, c] = st; }

    public int Count(SeatState st)
    {
        var n = 0;
        foreach (var x in _state) if (x == st) n++;
        return n;
    }

    /// <summary>
    /// True when the selection would leave a single empty seat stranded between
    /// taken seats (or a wall) in any affected row — the classic "no orphan seat" rule.
    /// Orphans that already existed before this selection (e.g. after an expired
    /// hold or a cancellation) do not block unrelated selections in the same row.
    /// </summary>
    public bool LeavesOrphanSeat(IReadOnlyCollection<SeatId> selection)
    {
        foreach (var row in selection.Select(s => s.Row).Distinct())
        {
            var r = row - 'A';
            bool TakenBefore(int c) => c < 0 || c >= SeatsPerRow || _state[r, c] != SeatState.Available;
            bool TakenAfter(int c) => TakenBefore(c) || selection.Contains(new SeatId(row, c + 1));
            for (var c = 0; c < SeatsPerRow; c++)
            {
                var orphanAfter = !TakenAfter(c) && TakenAfter(c - 1) && TakenAfter(c + 1);
                var orphanBefore = !TakenBefore(c) && TakenBefore(c - 1) && TakenBefore(c + 1);
                if (orphanAfter && !orphanBefore) return true;
            }
        }
        return false;
    }
}

public sealed record PriceList(decimal Standard, decimal Premium, decimal Vip, decimal FeePerTicket)
{
    public decimal Of(SeatTier t) => t switch { SeatTier.Premium => Premium, SeatTier.Vip => Vip, _ => Standard };
}

public sealed record Quote(IReadOnlyList<SeatId> Seats, decimal Subtotal, decimal Discount, decimal Fees, decimal Total);

public sealed record Hold(string Reference, IReadOnlyList<SeatId> Seats, DateTimeOffset ExpiresAt);

public sealed class BookingService
{
    public const int MaxSeatsPerBooking = 8;
    private readonly SeatMap _map;
    private readonly PriceList _prices;
    private readonly TimeSpan _holdFor;
    private readonly Dictionary<string, Hold> _holds = new();
    private long _sequence;

    public BookingService(SeatMap map, PriceList prices, TimeSpan holdFor)
    {
        if (holdFor <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(holdFor), "hold duration must be positive");
        _map = map; _prices = prices; _holdFor = holdFor;
    }

    private static readonly char[] Invisible = { '\u200B', '\u200C', '\u200D', '\u2060', '\uFEFF' };

    /// <summary>Trims and drops zero-width characters that pasted codes often carry.</summary>
    private static string Clean(string text) =>
        new string(text.Where(c => Array.IndexOf(Invisible, c) < 0).ToArray()).Trim();

    /// <summary>Promo codes: percentage off the subtotal (fees are never discounted).</summary>
    public static readonly IReadOnlyDictionary<string, int> PromoPercent = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["EARLYBIRD"] = 20,
        ["STUDENT"] = 10,
    };

    public Quote Price(IReadOnlyList<SeatId> seats, string? promo = null)
    {
        // Same rule as HoldSeats: a seat listed twice must not be charged twice.
        if (seats.Distinct().Count() != seats.Count) throw new ArgumentException("duplicate seat in selection");
        var subtotal = seats.Sum(s => _prices.Of(_map.TierOf(s)));
        var pct = promo is not null && PromoPercent.TryGetValue(Clean(promo), out var p) ? p : 0;
        var discount = Math.Round(subtotal * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var fees = _prices.FeePerTicket * seats.Count;
        return new Quote(seats, subtotal, discount, fees, subtotal - discount + fees);
    }

    /// <summary>Holds seats for a limited time after validating availability and the orphan rule.</summary>
    public Hold HoldSeats(IReadOnlyList<SeatId> seats, DateTimeOffset now)
    {
        ReleaseExpired(now);
        if (seats.Count == 0) throw new ArgumentException("select at least one seat");
        if (seats.Count > MaxSeatsPerBooking) throw new ArgumentException($"at most {MaxSeatsPerBooking} seats per booking");
        if (seats.Distinct().Count() != seats.Count) throw new ArgumentException("duplicate seat in selection");
        var unavailable = seats.Where(s => _map.StateOf(s) != SeatState.Available).ToList();
        if (unavailable.Count > 0) throw new InvalidOperationException($"not available: {string.Join(", ", unavailable)}");
        if (_map.LeavesOrphanSeat(seats)) throw new InvalidOperationException("selection would leave a single empty seat");
        foreach (var s in seats) _map.Set(s, SeatState.Held);
        var hold = new Hold(NextReference(), seats.ToList(), now + _holdFor);
        _holds[hold.Reference] = hold;
        return hold;
    }

    /// <summary>Confirms a hold; fails if it expired (seats are released).</summary>
    public void Confirm(string reference, DateTimeOffset now)
    {
        ReleaseExpired(now);
        // References are shown to the user, who may retype them in lower case or with spaces.
        reference = Clean(reference).ToUpperInvariant();
        if (!_holds.Remove(reference, out var hold)) throw new InvalidOperationException($"hold {reference} not found or expired");
        foreach (var s in hold.Seats) _map.Set(s, SeatState.Booked);
    }

    public void ReleaseExpired(DateTimeOffset now)
    {
        foreach (var h in _holds.Values.Where(h => h.ExpiresAt <= now).ToList())
        {
            foreach (var s in h.Seats) _map.Set(s, SeatState.Available);
            _holds.Remove(h.Reference);
        }
    }

    private string NextReference()
    {
        // Short, unambiguous (no 0/O/1/I) reference derived from a counter; unique per service instance.
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        // 7919 is odd, so seq -> (seq * 7919 + 104729) mod 32^6 is a bijection: no repeats
        // for 32^6 (~1.07 billion) holds, and no int overflow / negative index.
        const long space = 1L << 30; // 32^6
        var n = (++_sequence * 7919L + 104729L) % space;
        var chars = new char[6];
        for (var i = 5; i >= 0; i--) { chars[i] = alphabet[(int)(n % alphabet.Length)]; n /= alphabet.Length; }
        return new string(chars);
    }
}
