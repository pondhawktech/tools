// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Pondhawk.Api.Identity;

/// <summary>Converts between the wire <see cref="ClaimSet"/> and a <see cref="ClaimsPrincipal"/>.</summary>
internal static class ClaimSetPrincipal
{
    public static ClaimsPrincipal ToPrincipal(ClaimSet claims, string authenticationType)
    {
        var list = new List<Claim>();
        Add(list, IdentityConstants.SubjectClaim, claims.UserId);
        Add(list, IdentityConstants.UserNameClaim, claims.UserName);
        Add(list, IdentityConstants.FirstNameClaim, claims.FirstName);
        Add(list, IdentityConstants.LastNameClaim, claims.LastName);
        Add(list, IdentityConstants.EmailClaim, claims.Email);

        foreach (var role in claims.Roles)
            Add(list, IdentityConstants.RoleClaim, role);

        var identity = new ClaimsIdentity(
            list, authenticationType, IdentityConstants.UserNameClaim, IdentityConstants.RoleClaim);

        return new ClaimsPrincipal(identity);
    }

    public static ClaimSet FromPrincipal(ClaimsPrincipal principal) => new()
    {
        UserId = principal.FindFirstValue(IdentityConstants.SubjectClaim) ?? string.Empty,
        UserName = principal.FindFirstValue(IdentityConstants.UserNameClaim) ?? string.Empty,
        FirstName = principal.FindFirstValue(IdentityConstants.FirstNameClaim) ?? string.Empty,
        LastName = principal.FindFirstValue(IdentityConstants.LastNameClaim) ?? string.Empty,
        Email = principal.FindFirstValue(IdentityConstants.EmailClaim) ?? string.Empty,
        Roles = principal.FindAll(IdentityConstants.RoleClaim).Select(c => c.Value).ToList(),
    };

    private static void Add(List<Claim> claims, string type, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            claims.Add(new Claim(type, value));
    }
}
