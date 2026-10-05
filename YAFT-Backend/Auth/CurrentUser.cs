using System.Security.Claims;
using YAFT.Application.Abstractions;

namespace YAFT.Api.Auth;

/// <summary>Utente corrente ricavato dal Firebase ID token validato da JwtBearer.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Firebase inserisce l'UID nei claim "user_id" e "sub" (quest'ultimo mappato su NameIdentifier).
    public string? UserId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            return user.FindFirstValue("user_id")
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? user.FindFirstValue("sub");
        }
    }
}
