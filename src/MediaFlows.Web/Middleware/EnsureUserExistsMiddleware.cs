using System.Security.Claims;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using MediaFlows.Data;
using MediaFlows.Shared.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediaFlows.Web.Middleware;

public class EnsureUserExistsMiddleware
{
    private readonly RequestDelegate _next;

    public EnsureUserExistsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext db,
        IAmazonCognitoIdentityProvider cognito,
        ILogger<EnsureUserExistsMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var cognitoSub = context.User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(cognitoSub))
        {
            await _next(context);
            return;
        }

        var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.CognitoSub == cognitoSub);

        // Cognito groups (transformed into Role claims by CognitoGroupsClaimsTransformation)
        // are the source of truth. Used below both to seed Role on first insert and to
        // keep the stored Role column in sync when group membership changes.
        var claimRole = context.User.FindFirstValue(ClaimTypes.Role);

        // When profile fetches fail we previously wrote `cognitoSub` as a fallback
        // into Email/DisplayName to satisfy the unique index. That value is a GUID
        // and the old backfill only ran while those fields were empty, so a row
        // stuck with a sentinel never self-healed. Treat the sentinel as "needs
        // backfill" so the next request with proper claims repairs the row.
        bool IsSubSentinel(string? value) => !string.IsNullOrWhiteSpace(value) && value == cognitoSub;

        var needsProfileBackfill = existing != null
            && (string.IsNullOrWhiteSpace(existing.Email)
                || string.IsNullOrWhiteSpace(existing.DisplayName)
                || IsSubSentinel(existing.Email)
                || IsSubSentinel(existing.DisplayName));

        // Sync the DB Role column when Cognito group membership changes outside
        // the admin UI (direct console, post-confirmation Lambda, etc).
        var needsRoleSync = existing != null
            && !string.IsNullOrWhiteSpace(claimRole)
            && existing.Role != claimRole;

        if (existing == null || needsProfileBackfill || needsRoleSync)
        {
            // Try claims first (works for ID-token-derived principals, e.g. the OIDC
            // hosted UI flow). Fall back to a Cognito GetUser call when the request
            // is bearing only an access token, which doesn't carry email/name claims.
            var email = context.User.FindFirstValue("email");
            var displayName = context.User.FindFirstValue("name")
                ?? context.User.FindFirstValue("cognito:username");

            // Only hit Cognito when we'll actually write profile fields — creating a
            // new row or backfilling an existing one. A pure role sync on an existing
            // user with a complete profile doesn't need an extra round-trip.
            var willWriteProfile = existing == null || needsProfileBackfill;
            if (willWriteProfile && string.IsNullOrWhiteSpace(email))
            {
                var (fetchedEmail, fetchedName) = await TryGetCognitoAttributesAsync(context, cognito, logger);
                email = fetchedEmail;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = fetchedName;
            }

            if (existing == null)
            {
                db.AppUsers.Add(new AppUser
                {
                    CognitoSub = cognitoSub,
                    // Fall back to sub (always unique) so the unique index on Email
                    // never collides if GetUser fails for some reason. Subsequent
                    // requests will heal this via the sentinel check above.
                    Email = !string.IsNullOrWhiteSpace(email) ? email : cognitoSub,
                    DisplayName = !string.IsNullOrWhiteSpace(displayName)
                        ? displayName
                        : (!string.IsNullOrWhiteSpace(email) ? email : cognitoSub),
                    Role = !string.IsNullOrWhiteSpace(claimRole) ? claimRole : "Viewer",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                });
            }
            else
            {
                // Profile backfill — also heals rows where Email/DisplayName were
                // set to the cognitoSub sentinel by a previous fallback.
                if ((string.IsNullOrWhiteSpace(existing.Email) || IsSubSentinel(existing.Email))
                    && !string.IsNullOrWhiteSpace(email))
                    existing.Email = email;

                if (string.IsNullOrWhiteSpace(existing.DisplayName) || IsSubSentinel(existing.DisplayName))
                {
                    if (!string.IsNullOrWhiteSpace(displayName))
                        existing.DisplayName = displayName;
                    else if (!string.IsNullOrWhiteSpace(email))
                        existing.DisplayName = email;
                }

                if (needsRoleSync)
                    existing.Role = claimRole!;
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Failed to sync AppUser for sub {Sub}", cognitoSub);
            }
        }

        await _next(context);
    }

    private static async Task<(string? email, string? displayName)> TryGetCognitoAttributesAsync(
        HttpContext context,
        IAmazonCognitoIdentityProvider cognito,
        ILogger logger)
    {
        var auth = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        var accessToken = auth.Substring("Bearer ".Length).Trim();
        if (string.IsNullOrEmpty(accessToken))
            return (null, null);

        try
        {
            var resp = await cognito.GetUserAsync(new GetUserRequest { AccessToken = accessToken });
            var email = resp.UserAttributes.FirstOrDefault(a => a.Name == "email")?.Value;
            var name = resp.UserAttributes.FirstOrDefault(a => a.Name == "name")?.Value;
            return (email, name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Cognito user attributes for sync");
            return (null, null);
        }
    }
}
