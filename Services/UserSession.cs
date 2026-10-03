namespace RBI_Malaysia.Services
{
    public class UserSession
    {
        public string UserID { get; set; } = "";
        public string UserName { get; set; } = "";
        public string CompanyID { get; set; } = "";
        public string CompanyName { get; set; } = "";

        public event Action? OnChange;

        public bool IsLoggedIn
        {
            get
            {
                return decimal.TryParse(UserID, out decimal userId)
                       && userId > 0
                       && !string.IsNullOrWhiteSpace(UserName);
            }
        }

        public void SetSession(string userId, string userName, string companyId, string companyName)
        {
            UserID = userId;
            UserName = userName;
            CompanyID = companyId;
            CompanyName = companyName;

            NotifyStateChanged();
        }

        public void Clear()
        {
            UserID = "";
            UserName = "";
            CompanyID = "";
            CompanyName = "";

            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }
}
