using System.Security.Claims;
using Pondhawk.Api.Identity;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Identity;

public class ClaimSetPrincipalTests
{
    [Fact]
    public void ToPrincipal_FromPrincipal_RoundTrips()
    {
        var claims = TestKeys.SampleClaimSet();

        var principal = ClaimSetPrincipal.ToPrincipal(claims, IdentityConstants.Scheme);
        var back = ClaimSetPrincipal.FromPrincipal(principal);

        back.UserId.ShouldBe(claims.UserId);
        back.UserName.ShouldBe(claims.UserName);
        back.FirstName.ShouldBe(claims.FirstName);
        back.LastName.ShouldBe(claims.LastName);
        back.Email.ShouldBe(claims.Email);
        back.Roles.ShouldBe(claims.Roles);
    }

    [Fact]
    public void ToPrincipal_MapsRolesToRoleClaims()
    {
        var claims = TestKeys.SampleClaimSet();

        var principal = ClaimSetPrincipal.ToPrincipal(claims, IdentityConstants.Scheme);

        principal.IsInRole("admin").ShouldBeTrue();
        principal.IsInRole("user").ShouldBeTrue();
        principal.FindAll(IdentityConstants.RoleClaim).Select(c => c.Value)
            .ShouldBe(new[] { "admin", "user" });
    }

    [Fact]
    public void ToPrincipal_OmitsEmptyFields()
    {
        var claims = new ClaimSet { UserId = "only-id" };

        var principal = ClaimSetPrincipal.ToPrincipal(claims, IdentityConstants.Scheme);
        var identity = (ClaimsIdentity)principal.Identity;

        identity.FindFirst(IdentityConstants.SubjectClaim).ShouldNotBeNull();
        identity.FindFirst(IdentityConstants.UserNameClaim).ShouldBeNull();
        identity.FindFirst(IdentityConstants.EmailClaim).ShouldBeNull();
        identity.FindAll(IdentityConstants.RoleClaim).ShouldBeEmpty();
    }

    [Fact]
    public void FromPrincipal_MissingClaims_YieldEmptyStrings()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var cs = ClaimSetPrincipal.FromPrincipal(principal);

        cs.UserId.ShouldBe(string.Empty);
        cs.Email.ShouldBe(string.Empty);
        cs.Roles.ShouldBeEmpty();
    }
}
