namespace YAFT.Application.Abstractions;

public interface ICurrentUser
{
    /// UID Firebase dell'utente autenticato, oppure null.
    string? UserId { get; }
}
