// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Api.Identity;

/// <summary>
/// Well-known header, scheme, policy, and claim names for gateway authentication.
/// </summary>
public static class IdentityConstants
{
    /// <summary>Header carrying an unsigned JSON claim set (header mode).</summary>
    public const string IdentityHeaderName = "X-Gateway-Identity";

    /// <summary>Header carrying a signed JWT claim set (token mode).</summary>
    public const string TokenHeaderName = "X-Gateway-Identity-Token";

    /// <summary>The single authentication scheme name (both handlers share it).</summary>
    public const string Scheme = "Pondhawk.GatewayToken";

    /// <summary>Policy allowing anonymous/public access.</summary>
    public const string PublicPolicy = "AllowPublic";

    /// <summary>Policy requiring the admin role.</summary>
    public const string AdminPolicy = "RequiresAdminRole";

    /// <summary>The admin role name.</summary>
    public const string AdminRole = "admin";

    // Standard JWT claim names for the minimal claim set.

    /// <summary>Subject / user identity claim (<c>sub</c>).</summary>
    public const string SubjectClaim = "sub";

    /// <summary>User name claim (<c>preferred_username</c>).</summary>
    public const string UserNameClaim = "preferred_username";

    /// <summary>First name claim (<c>given_name</c>).</summary>
    public const string FirstNameClaim = "given_name";

    /// <summary>Last name claim (<c>family_name</c>).</summary>
    public const string LastNameClaim = "family_name";

    /// <summary>Email claim (<c>email</c>).</summary>
    public const string EmailClaim = "email";

    /// <summary>Role claim (<c>role</c>), repeated per role.</summary>
    public const string RoleClaim = "role";
}
