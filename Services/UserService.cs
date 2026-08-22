using Microsoft.Data.SqlClient;
using RBI_Malaysia.Components.Pages;
using System.Data;

namespace RBI_Malaysia.Services
{
    public class UserService
    {
        private readonly IConfiguration _configuration;

        public UserService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("connString")
            ?? throw new Exception("Connection string connString was not found.");


        // =========================================================
        // GET USERS
        // =========================================================
        public async Task<List<UserModel>> GetUsersAsync()
        {
            var users = new List<UserModel>();

            using SqlConnection conn = new SqlConnection(ConnectionString);

            await conn.OpenAsync();
            using SqlCommand cmd = new SqlCommand("sp_Get_tbl_Val", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@id", 0);
            cmd.Parameters.AddWithValue("@tblflg", "UsrGrid");

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new UserModel
                {
                    ID = reader["ID"]?.ToString() ?? "",
                    UserID = reader["UserID"]?.ToString() ?? "",
                    Password = reader["Password"]?.ToString() ?? "",

                    StaffId = reader["StaffId"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["StaffId"]),

                    StaffName =
                        reader["StaffName"]?.ToString() ?? "",

                    Company = reader["Company"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["Company"]),

                    CompanyName =
                        reader["CompanyName"]?.ToString() ?? ""
                });
            }
            await conn.CloseAsync();
            return users;
        }


        // =========================================================
        // GET STAFF
        // =========================================================
        public async Task<List<StaffModel>> GetStaffsAsync(
       decimal currentStaffId = 0)
        {
            var staffs = new List<StaffModel>();

            using SqlConnection conn = new SqlConnection(ConnectionString);

            await conn.OpenAsync();

            using SqlCommand cmd = new SqlCommand("sp_Get_tbl_Val", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@id", currentStaffId);
            cmd.Parameters.AddWithValue("@tblflg", "UsrStfSel");

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                staffs.Add(new StaffModel
                {
                    StaffId = Convert.ToDecimal(reader["StaffId"]),
                    StaffName = reader["StaffName"]?.ToString() ?? ""
                });
            }
            await conn.CloseAsync();
            return staffs;
        }


        // =========================================================
        // GET COMPANY
        // =========================================================
        public async Task<List<CompanyModel>> GetCompaniesAsync()
        {
            var companies = new List<CompanyModel>();

            using SqlConnection conn = new SqlConnection(ConnectionString);

            await conn.OpenAsync();

            using SqlCommand cmd = new SqlCommand("sp_Get_tbl_Val", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@id", 0);
            cmd.Parameters.AddWithValue("@tblflg", "UsrCmySel");

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                companies.Add(new CompanyModel
                {
                    CompanyID = Convert.ToInt32(reader["CompanyId"]),
                    CompanyName = reader["CompanyName"]?.ToString() ?? ""
                });
            }
            await conn.CloseAsync();
            return companies;
        }


        // =========================================================
        // INSERT / UPDATE / DELETE
        // =========================================================
        public async Task<int> SaveUserAsync(
            UserModel user,
            string saveFlag)
        {
            using SqlConnection conn =
                new SqlConnection(ConnectionString);

            await conn.OpenAsync();

            string procedureName = saveFlag switch
            {
                "Insert" => "sp_user_tbl_insert",
                "Update" => "sp_user_tbl_update",
                "Delete" => "sp_user_tbl_delete",
                _ => throw new ArgumentException(
                    "Invalid save flag.")
            };

            using SqlCommand cmd =
                new SqlCommand(procedureName, conn);

            cmd.CommandType =
                CommandType.StoredProcedure;


            // -----------------------------------------------------
            // UPDATE / DELETE
            // -----------------------------------------------------
            if (saveFlag == "Update" ||
                saveFlag == "Delete")
            {
                cmd.Parameters.AddWithValue(
                    "@id",
                    user.ID);
            }


            // -----------------------------------------------------
            // INSERT / UPDATE
            // -----------------------------------------------------
            if (saveFlag == "Insert" ||
                saveFlag == "Update")
            {
                cmd.Parameters.AddWithValue(
                    "@StaffId",
                    user.StaffId);

                cmd.Parameters.AddWithValue(
                    "@StaffName",
                    user.StaffName.Trim());

                cmd.Parameters.AddWithValue(
                    "@UserID",
                    user.UserID.Trim());

                cmd.Parameters.AddWithValue(
                    "@Password",
                    user.Password.Trim());

                cmd.Parameters.AddWithValue(
                    "@Company",
                    user.Company);

                cmd.Parameters.AddWithValue(
                    "@CompanyName",
                    user.CompanyName.Trim());

                cmd.Parameters.AddWithValue(
                    "@Createdby",
                    user.Createdby ?? "");
            }

            return await cmd.ExecuteNonQueryAsync();
        }
    }


    // =============================================================
    // USER MODEL
    // =============================================================
    public class UserModel
    {
        public string ID { get; set; } = "";

        public string UserID { get; set; } = "";

        public string Password { get; set; } = "";

        public decimal StaffId { get; set; }

        public string StaffName { get; set; } = "";

        public decimal Company { get; set; }

        public string CompanyName { get; set; } = "";

        public string Createdby { get; set; } = "";
    }



}