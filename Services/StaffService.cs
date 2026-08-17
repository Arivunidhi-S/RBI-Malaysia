using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace RBI_Malaysia.Services
{
    public class StaffService
    {
        private readonly IConfiguration _configuration;

        public StaffService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            string connectionString =
                _configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException(
                    "Connection string 'connString' not found.");

            return new SqlConnection(connectionString);
        }


        // =========================================================
        // SAVE / INSERT / UPDATE
        // =========================================================
        public async Task<int> SaveStaffAsync(
            decimal staffId,
            string staffNo,
            string staffName,
            string designation,
            string address,
            string phone,
            string email,
            decimal userId,
            string saveFlag)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Staff_Save", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@idp", staffId);
            cmd.Parameters.AddWithValue("@staffnop", staffNo ?? "");
            cmd.Parameters.AddWithValue("@staffnamep", staffName ?? "");
            cmd.Parameters.AddWithValue("@designationp", designation ?? "");
            cmd.Parameters.AddWithValue("@addressp", address ?? "");
            cmd.Parameters.AddWithValue("@phonep", phone ?? "");
            cmd.Parameters.AddWithValue("@emailp", email ?? "");
            cmd.Parameters.AddWithValue("@useridp", userId);
            cmd.Parameters.AddWithValue("@saveflag", saveFlag ?? "");

            return await cmd.ExecuteNonQueryAsync();
        }


        // =========================================================
        // GET ALL STAFF
        // =========================================================
        public async Task<List<StaffModel>> GetStaffsAsync()
        {
            var staffList = new List<StaffModel>();

            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Staff_Get", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                staffList.Add(new StaffModel
                {
                    StaffId = reader["StaffId"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["StaffId"]),

                    StaffNo = reader["StaffNo"]?.ToString() ?? "",

                    StaffName = reader["StaffName"]?.ToString() ?? "",

                    Designation = reader["Designation"]?.ToString() ?? "",

                    Address = reader["Address"]?.ToString() ?? "",

                    Phone = reader["Phone"]?.ToString() ?? "",

                    Email = reader["Email"]?.ToString() ?? "",

                    Createdby = reader["Createdby"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["Createdby"]),

                    Createddate = reader["Createddate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["Createddate"]),

                    Modifiedby = reader["Modifiedby"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["Modifiedby"]),

                    Modifieddate = reader["Modifieddate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["Modifieddate"]),

                    Rowversions = reader["Rowversions"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["Rowversions"]),

                    Deleted = reader["Deleted"] != DBNull.Value
                        && Convert.ToBoolean(reader["Deleted"])
                });
            }

            return staffList;
        }


        // =========================================================
        // GET STAFF BY ID
        // =========================================================
        public async Task<StaffModel?> GetStaffByIdAsync(decimal staffId)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Staff_GetById", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@idp", staffId);

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new StaffModel
                {
                    StaffId = Convert.ToDecimal(reader["StaffId"]),

                    StaffNo = reader["StaffNo"]?.ToString() ?? "",

                    StaffName = reader["StaffName"]?.ToString() ?? "",

                    Designation = reader["Designation"]?.ToString() ?? "",

                    Address = reader["Address"]?.ToString() ?? "",

                    Phone = reader["Phone"]?.ToString() ?? "",

                    Email = reader["Email"]?.ToString() ?? ""
                };
            }

            return null;
        }


        // =========================================================
        // DELETE STAFF - SOFT DELETE
        // =========================================================
        public async Task<bool> DeleteStaffAsync(
            decimal staffId,
            decimal userId)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Staff_Delete", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@idp", staffId);
            cmd.Parameters.AddWithValue("@useridp", userId);

            int result = await cmd.ExecuteNonQueryAsync();

            return result > 0;
        }

        public async Task<string> GenerateStaffNoAsync(
         string designation)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_Staff_GenerateStaffNo", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@Designation",
                designation ?? "");

            object? result = await cmd.ExecuteScalarAsync();

            return result?.ToString() ?? "";
        }

    }

     

    // =============================================================
    // STAFF MODEL
    // =============================================================
    public class StaffModel
    {
        public decimal StaffId { get; set; }

        public string StaffNo { get; set; } = "";

        [Required(ErrorMessage = "Staff name is required.")]
        public string StaffName { get; set; } = "";

        [Required(ErrorMessage = "Please select designation.")]
        public string Designation { get; set; } = "";

        public string Address { get; set; } = "";

        [Required(ErrorMessage = "Mobile number is required.")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";

        public decimal Createdby { get; set; }

        public DateTime? Createddate { get; set; }

        public decimal Modifiedby { get; set; }

        public DateTime? Modifieddate { get; set; }

        public int Rowversions { get; set; }

        public bool Deleted { get; set; }
    }
}

