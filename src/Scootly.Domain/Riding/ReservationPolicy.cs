namespace Scootly.Domain.Riding;

public sealed class ReservationPolicy
{
    public const int ReservationDurationMinutes = 10;

    public bool IsExpired(Reservation reservation, DateTime now)
    {
        return now > reservation.ExpiresAt;
    }
}