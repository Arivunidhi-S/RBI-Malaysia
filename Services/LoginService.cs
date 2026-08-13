using Microsoft.Data.SqlClient;
using System.Data;

namespace RBI_Malaysia.Services;

public class LoginService
{
    private readonly IConfiguration _configuration;

    public LoginService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<LoginResult?> ValidateLoginAsync(
        string username,
        string password)
    {
        string connectionString =
            _configuration.GetConnectionString("connString")
            ?? throw new InvalidOperationException(
                "Connection string 'connString' was not found.");

        await using SqlConnection connection =
            new SqlConnection(connectionString);

        await connection.OpenAsync();

        await using SqlCommand command =
            new SqlCommand("sp_Validate_UserLogin", connection);

        command.CommandType = CommandType.StoredProcedure;

        // Same parameters used by the old BusinessTier
        command.Parameters.AddWithValue("@Useridp", username);
        command.Parameters.AddWithValue("@Passp", password);

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            string userId = reader["ID"]?.ToString()?.Trim() ?? "";

            // Old application checks ID before considering login successful
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            return new LoginResult
            {
                UserID = userId,
                UserName = reader["StaffName"]?.ToString()?.Trim() ?? "",
                CompanyID = reader["Company"]?.ToString()?.Trim() ?? "",
                CompanyName = reader["CompanyName"]?.ToString()?.Trim() ?? ""
            };
        }

        return null;
    }
}


public class LoginResult
{
    public string UserID { get; set; } = "";

    public string UserName { get; set; } = "";

    public string CompanyID { get; set; } = "";

    public string CompanyName { get; set; } = "";
}