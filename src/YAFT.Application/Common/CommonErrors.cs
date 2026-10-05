namespace YAFT.Application.Common;

public static class CommonErrors
{
    public static readonly Error Unauthenticated =
        new("auth.required", "Utente non autenticato.", ErrorType.Unauthorized);
}
