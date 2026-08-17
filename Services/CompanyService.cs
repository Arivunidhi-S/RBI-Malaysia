using System.Data;
using Microsoft.Data.SqlClient;

namespace RBI_Malaysia.Services
{
    public class CompanyService
    {
        private readonly IConfiguration _configuration;

        public CompanyService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =========================================================
        // CONNECTION
        // =========================================================

        private SqlConnection GetConnection()
        {
            string connectionString =
                _configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException(
                    "Connection string 'connString' was not found.");

            return new SqlConnection(connectionString);
        }


        // =========================================================
        // GET ALL COMPANIES
        // =========================================================

        public async Task<List<CompanyModel>> GetCompaniesAsync()
        {
            List<CompanyModel> companies = new();

            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            string sql = @"
                SELECT
                    CompanyID,
                    CompanyName,
                    Address1,
                    Address2,
                    City,
                    State,
                    Country,
                    Postcode,
                    Description,
                    ContactNo,
                    FaxNo,
                    Email,
                    Website
                FROM Company WHERE deleted=0
                ORDER BY CompanyID DESC";

            await using SqlCommand cmd = new SqlCommand(sql, conn);

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                companies.Add(new CompanyModel
                {
                    CompanyID = reader["CompanyID"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["CompanyID"]),

                    CompanyName = reader["CompanyName"]?.ToString() ?? "",

                    Address1 = reader["Address1"]?.ToString() ?? "",

                    Address2 = reader["Address2"]?.ToString() ?? "",

                    City = reader["City"]?.ToString() ?? "",

                    State = reader["State"]?.ToString() ?? "",

                    Country = reader["Country"]?.ToString() ?? "",

                    Postcode = reader["Postcode"]?.ToString() ?? "",

                    Description = reader["Description"]?.ToString() ?? "",

                    Phone = reader["ContactNo"]?.ToString() ?? "",

                    Fax = reader["FaxNo"]?.ToString() ?? "",

                    Email = reader["Email"]?.ToString() ?? "",

                    Website = reader["Website"]?.ToString() ?? ""
                });
            }

            return companies;
        }


        // =========================================================
        // SAVE / INSERT / UPDATE
        // =========================================================

        public async Task<int> SaveCompanyAsync(
            CompanyModel company,
            string userid,
            string saveflag)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Company_Save", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@idp",
                company.CompanyID.ToString());

            cmd.Parameters.AddWithValue(
                "@namep",
                company.CompanyName ?? "");

            cmd.Parameters.AddWithValue(
                "@address1p",
                company.Address1 ?? "");

            cmd.Parameters.AddWithValue(
                "@address2p",
                company.Address2 ?? "");

            cmd.Parameters.AddWithValue(
                "@cityp",
                company.City ?? "");

            cmd.Parameters.AddWithValue(
                "@statep",
                company.State ?? "");

            cmd.Parameters.AddWithValue(
                "@countryp",
                company.Country ?? "");

            cmd.Parameters.AddWithValue(
                "@descriptionp",
                company.Description ?? "");

            cmd.Parameters.AddWithValue(
                "@contactnop",
                company.Phone ?? "");

            cmd.Parameters.AddWithValue(
                "@faxnop",
                company.Fax ?? "");

            cmd.Parameters.AddWithValue(
                "@emailp",
                company.Email ?? "");

            cmd.Parameters.AddWithValue(
                "@websitep",
                company.Website ?? "");

            cmd.Parameters.AddWithValue(
                "@postcodep",
                company.Postcode ?? "");

            cmd.Parameters.AddWithValue(
                "@useridp",
                userid ?? "");

            cmd.Parameters.AddWithValue(
                "@saveflag",
                saveflag ?? "");

            return await cmd.ExecuteNonQueryAsync();
        }


        // =========================================================
        // DELETE COMPANY
        // =========================================================

        public async Task<bool> DeleteCompanyAsync(
            int CompanyID,
            string userid)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            string sql = @"update Company set deleted=1,Modifiedby=@userid WHERE CompanyID=@CompanyID";

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@CompanyID", CompanyID);
            cmd.Parameters.AddWithValue("@userid", userid);

            int result = await cmd.ExecuteNonQueryAsync();

            return result > 0;
        }
    }
}