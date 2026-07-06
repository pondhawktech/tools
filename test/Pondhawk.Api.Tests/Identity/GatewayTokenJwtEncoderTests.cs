using Pondhawk.Api.Identity;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Identity;

public class GatewayTokenJwtEncoderTests
{
    [Fact]
    public void Ctor_NullKey_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new GatewayTokenJwtEncoder(null));
    }

    [Fact]
    public async Task EncodeDecode_RoundTripsAllFields()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var claims = TestKeys.SampleClaimSet();

        var token = encoder.Encode(claims);
        token.ShouldNotBeNullOrWhiteSpace();

        var decoded = await encoder.DecodeAsync(token);

        decoded.ShouldNotBeNull();
        decoded.UserId.ShouldBe(claims.UserId);
        decoded.UserName.ShouldBe(claims.UserName);
        decoded.FirstName.ShouldBe(claims.FirstName);
        decoded.LastName.ShouldBe(claims.LastName);
        decoded.Email.ShouldBe(claims.Email);
        decoded.Roles.ShouldBe(claims.Roles);
    }

    [Fact]
    public void Encode_NullClaims_Throws()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        Should.Throw<ArgumentNullException>(() => encoder.Encode(null));
    }

    [Fact]
    public async Task Decode_EmptyRoles_ProducesEmptyRoleList()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var claims = new ClaimSet { UserId = "u-1" };

        var token = encoder.Encode(claims);
        var decoded = await encoder.DecodeAsync(token);

        decoded.ShouldNotBeNull();
        decoded.UserId.ShouldBe("u-1");
        decoded.Roles.ShouldBeEmpty();
        decoded.UserName.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Decode_ExpiredToken_ReturnsNull()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey)
        {
            TokenTimeToLive = TimeSpan.FromSeconds(-30),
        };

        var token = encoder.Encode(TestKeys.SampleClaimSet());
        var decoded = await encoder.DecodeAsync(token);

        decoded.ShouldBeNull();
    }

    [Fact]
    public async Task Decode_WrongKey_ReturnsNull()
    {
        var signer = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var verifier = new GatewayTokenJwtEncoder(TestKeys.OtherKey);

        var token = signer.Encode(TestKeys.SampleClaimSet());
        var decoded = await verifier.DecodeAsync(token);

        decoded.ShouldBeNull();
    }

    [Fact]
    public async Task Decode_Garbage_ReturnsNull()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var decoded = await encoder.DecodeAsync("not-a-jwt");
        decoded.ShouldBeNull();
    }
}
