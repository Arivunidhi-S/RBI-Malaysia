namespace RBI_Malaysia.Services;

public class UserSession
{
    public string UserID { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string CompanyID { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public bool IsLoggedIn =>
        !string.IsNullOrWhiteSpace(UserID);
}