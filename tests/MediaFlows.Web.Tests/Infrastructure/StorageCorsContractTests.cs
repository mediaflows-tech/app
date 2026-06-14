using FluentAssertions;

namespace MediaFlows.Web.Tests.Infrastructure;

public class StorageCorsContractTests
{
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    [Fact]
    public void RootTerraform_ShouldPassCorsAllowedOrigins_IntoStorageModule()
    {
        var variables = ReadRepoFile("infrastructure/variables.tf");
        var rootMain = ReadRepoFile("infrastructure/main.tf");

        variables.Should().Contain("variable \"cors_allowed_origins\"");
        rootMain.Should().Contain("cors_allowed_origins = var.cors_allowed_origins");
    }

    [Fact]
    public void ProductionTfvars_ShouldAllowTheProductionOrigin()
    {
        // Real prod.tfvars is gitignored; the committed .example file
        // documents the contract and is what CI inspects.
        var prodTfvars = ReadRepoFile("infrastructure/environments/prod.tfvars.example");

        prodTfvars.Should().Contain("cors_allowed_origins");
        // The frontend now serves at app.${domain} because the dead account
        // still globally claims the apex Amplify domain. Either host is
        // acceptable here — the apex or the app subdomain.
        prodTfvars.Should().MatchRegex("https://(app\\.)?mediaflows\\.tech");
    }
}
