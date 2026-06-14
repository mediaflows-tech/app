namespace MediaFlows.Shared.Configuration;

public class CognitoSettings
{
    public string Authority { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string Domain { get; set; } = null!;
    public string UserPoolId { get; set; } = null!;
    public string LogoutUri { get; set; } = null!;
}
