using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace MediaFlows.Web.Auth;

public class CognitoGroupsClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return Task.FromResult(principal);

        var groupClaims = identity.FindAll("cognito:groups").ToList();
        foreach (var groupClaim in groupClaims)
        {
            if (!identity.HasClaim(ClaimTypes.Role, groupClaim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, groupClaim.Value));
            }
        }

        return Task.FromResult(principal);
    }
}
