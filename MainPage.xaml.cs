using System.Globalization;
using TicketBooking.Core;

namespace TicketBooking;

public partial class MainPage : ContentPage
{
    private readonly List<SeatId> _selection = new();
    private readonly Dictionary<SeatId, Button> _buttons = new();
    private Hold? _hold;

    public MainPage()
    {
        InitializeComponent();
        BuildGrid();
        Render();
    }

    private void BuildGrid()
    {
        var map = BookingState.Map;
        for (var c = 0; c < map.SeatsPerRow; c++) SeatGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < map.Rows; r++)
        {
            SeatGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < map.SeatsPerRow; c++)
            {
                var seat = new SeatId((char)('A' + r), c + 1);
                var button = new Button
                {
                    Text = seat.ToString(),
                    FontSize = 10,
                    Padding = 0,
                    WidthRequest = 30,
                    HeightRequest = 30,
                    MinimumHeightRequest = 30,
                };
                SemanticProperties.SetDescription(button, $"Seat {seat}, {map.TierOf(seat)}");
                button.Clicked += (_, _) => Toggle(seat);
                _buttons[seat] = button;
                SeatGrid.Add(button, c, r);
            }
        }
    }

    private void Toggle(SeatId seat)
    {
        if (_hold is not null || BookingState.Map.StateOf(seat) != SeatState.Available) return;
        if (!_selection.Remove(seat))
        {
            if (_selection.Count >= BookingService.MaxSeatsPerBooking) return;
            _selection.Add(seat);
        }
        MessageLabel.Text = string.Empty;
        Render();
    }

    private void OnPromoChanged(object? sender, TextChangedEventArgs e) => Render();

    private void OnHoldClicked(object? sender, EventArgs e)
    {
        try
        {
            _hold = BookingState.Service.HoldSeats(_selection.ToList(), DateTimeOffset.Now);
            MessageLabel.Text = $"Held as {_hold.Reference} until {_hold.ExpiresAt.ToLocalTime():HH:mm}.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            MessageLabel.Text = ex.Message;
        }
        Render();
    }

    private void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (_hold is null) return;
        try
        {
            BookingState.Service.Confirm(_hold.Reference, DateTimeOffset.Now);
            MessageLabel.Text = $"Booked {string.Join(", ", _hold.Seats)} (ref {_hold.Reference}).";
        }
        catch (InvalidOperationException ex)
        {
            MessageLabel.Text = ex.Message;
        }
        _hold = null;
        _selection.Clear();
        Render();
    }

    private void Render()
    {
        foreach (var (seat, button) in _buttons)
        {
            var state = BookingState.Map.StateOf(seat);
            var selected = _selection.Contains(seat);
            button.BackgroundColor = selected ? Colors.DarkGreen
                : state != SeatState.Available ? Colors.LightGray
                : BookingState.Map.TierOf(seat) switch
                {
                    SeatTier.Vip => Colors.Goldenrod,
                    SeatTier.Premium => Colors.SteelBlue,
                    _ => Colors.SlateGray,
                };
            button.IsEnabled = state == SeatState.Available || selected;
        }

        if (_selection.Count == 0)
        {
            QuoteLabel.Text = $"Select up to {BookingService.MaxSeatsPerBooking} seats.";
        }
        else
        {
            var q = BookingState.Service.Price(_selection, PromoEntry.Text);
            var money = (decimal d) => d.ToString("N2", CultureInfo.CurrentCulture);
            QuoteLabel.Text = $"{string.Join(", ", q.Seats)} · subtotal {money(q.Subtotal)} − discount {money(q.Discount)} + fees {money(q.Fees)} = {money(q.Total)}";
        }
        HoldButton.IsEnabled = _hold is null && _selection.Count > 0;
        ConfirmButton.IsEnabled = _hold is not null;
    }
}
