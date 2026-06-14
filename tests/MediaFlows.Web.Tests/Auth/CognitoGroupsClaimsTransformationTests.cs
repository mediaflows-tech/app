using System.Security.Claims;
using FluentAssertions;
using MediaFlows.Web.Auth;

namespace MediaFlows.Web.Tests.Auth;

public class CognitoGroupsClaimsTransformationTests
{
    private readonly CognitoGroupsClaimsTransformation _sut = new();

    [Fact]
    public async Task TransformAsync_WithCognitoGroups_AddsRoleClaims()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "test-user-id"),
            new Claim("cognito:groups", "SystemAdmin"),
            new Claim("cognito:groups", "ContentCreator"),
        }, "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        result.IsInRole("SystemAdmin").Should().BeTrue();
        result.IsInRole("ContentCreator").Should().BeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithoutCognitoGroups_DoesNotAddRoleClaims()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "test-user-id"),
        }, "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        result.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_DoesNotDuplicateRoles()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "test-user-id"),
            new Claim("cognito:groups", "SystemAdmin"),
        }, "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        // Call twice to test idempotency
        await _sut.TransformAsync(principal);
        var result = await _sut.TransformAsync(principal);

        result.Claims
            .Count(c => c.Type == ClaimTypes.Role && c.Value == "SystemAdmin")
            .Should().Be(1);
    }

    [Fact]
    public async Task TransformAsync_UnauthenticatedPrincipal_ReturnsUnchanged()
    {
        var identity = new ClaimsIdentity();   // Not authenticated (no auth type)
        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        result.Claims.Where(c => c.Type == ClaimTypes.Role).Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_PreservesExistingRoleClaims()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "test-user-id"),
            new Claim(ClaimTypes.Role, "ExistingRole"),
            new Claim("cognito:groups", "SystemAdmin"),
        }, "TestAuth");

        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        result.IsInRole("ExistingRole").Should().BeTrue();
        result.IsInRole("SystemAdmin").Should().BeTrue();
    }
}
