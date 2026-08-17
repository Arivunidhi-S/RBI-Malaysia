namespace RBI_Malaysia.Services
{
    public class UserSession
    {
        public string UserID { get; set; } = "";
        public string UserName { get; set; } = "";
        public string CompanyID { get; set; } = "";
        public string CompanyName { get; set; } = "";

        public bool IsLoggedIn
        {
            get
            {
                return decimal.TryParse(UserID, out decimal userId)
                       && userId > 0
                       && !string.IsNullOrWhiteSpace(UserName);
            }
        }

        public void Clear()
        {
            UserID = "";
            UserName = "";
            CompanyID = "";
            CompanyName = "";
        }
    }
}