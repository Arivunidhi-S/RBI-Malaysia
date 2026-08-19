using System.Data;
using Microsoft.Data.SqlClient;


namespace RBI_Malaysia.Services
{
    public class ProcessAreaService
    {
        private readonly IConfiguration _configuration;

        public ProcessAreaService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =========================================================
        // CONNECTION STRING
        // =========================================================

        private string GetConnectionString()
        {
            return _configuration.GetConnectionString("connString")
                ?? throw new Exception(
                    "Connection string connString was not found.");
        }


        // =========================================================
        // GET PROCESS AREA LIST
        // =========================================================

        public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(
            decimal companyID)
        {
            var processAreas = new List<ProcessAreaModel>();

            using SqlConnection con =
                new SqlConnection(GetConnectionString());

            string sql = @"
    SELECT
        ProcessAreaID,
        ProcessArea,
        Description,
        ProcessUnit
    FROM Tbl_ProcessArea
    WHERE CompanyID = @CompanyID
      AND ISNULL(deleted, 0) = 0
    ORDER BY ProcessAreaID DESC";

            using SqlCommand cmd = new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@CompanyID",
                SqlDbType.Decimal).Value = companyID;

            await con.OpenAsync();

            using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                processAreas.Add(new ProcessAreaModel
                {
                    ProcessAreaID =
                        reader["ProcessAreaID"] == DBNull.Value
                            ? 0
                            : Convert.ToInt64(
                                reader["ProcessAreaID"]),

                    ProcessArea =
                        reader["ProcessArea"] == DBNull.Value
                            ? string.Empty
                            : reader["ProcessArea"].ToString()!,

                    Description =
                        reader["Description"] == DBNull.Value
                            ? string.Empty
                            : reader["Description"].ToString()!,

                    ProcessUnit =
                        reader["ProcessUnit"] == DBNull.Value
                            ? string.Empty
                            : reader["ProcessUnit"].ToString()!
                });
            }

            return processAreas;
        }


        // =========================================================
        // SAVE / UPDATE
        // Uses existing sp_ProcessArea
        // =========================================================

        public async Task SaveProcessAreaAsync(
            decimal processAreaID,
            string processArea,
            string description,
            string processUnit,
            decimal companyID,
            decimal userID,
            string saveFlag)
        {
            using SqlConnection con =
                new SqlConnection(GetConnectionString());

            using SqlCommand cmd =
                new SqlCommand("sp_ProcessArea", con);

            cmd.CommandType =
                CommandType.StoredProcedure;


            // -----------------------------------------------------
            // @idp
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@idp",
                SqlDbType.Decimal).Value = processAreaID;


            // -----------------------------------------------------
            // @processareap
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@processareap",
                SqlDbType.NVarChar, 50).Value =
                    processArea ?? string.Empty;


            // -----------------------------------------------------
            // @descriptionp
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@descriptionp",
                SqlDbType.NVarChar, 100).Value =
                    description ?? string.Empty;


            // -----------------------------------------------------
            // @unitp
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@unitp",
                SqlDbType.NVarChar, 50).Value =
                    processUnit ?? string.Empty;


            // -----------------------------------------------------
            // @CompanyID
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@CompanyID",
                SqlDbType.Decimal).Value =
                    companyID;


            // -----------------------------------------------------
            // @useridp
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@useridp",
                SqlDbType.Decimal).Value =
                    userID;


            // -----------------------------------------------------
            // @saveflag
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@saveflag",
                SqlDbType.NVarChar, 15).Value =
                    saveFlag;


            await con.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }


        // =========================================================
        // DELETE
        // =========================================================

        // =========================================================
        // DELETE PROCESS AREA
        // Uses existing sp_Delete
        // =========================================================

        public async Task DeleteProcessAreaAsync(
            decimal processAreaID)
        {
            using SqlConnection con =
                new SqlConnection(GetConnectionString());

            using SqlCommand cmd =
                new SqlCommand("sp_Delete", con);

            cmd.CommandType =
                CommandType.StoredProcedure;


            // -----------------------------------------------------
            // @id
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@id",
                SqlDbType.Decimal).Value =
                    processAreaID;


            // -----------------------------------------------------
            // @tblflg
            // -----------------------------------------------------

            cmd.Parameters.Add(
                "@tblflg",
                SqlDbType.NVarChar, 10).Value =
                    "process";


            await con.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }
    }
}