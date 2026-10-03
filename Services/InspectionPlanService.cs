using System.Data;
using Microsoft.Data.SqlClient;

namespace RBI_Malaysia.Services
{
    public class InspectionPlanService
    {
        private readonly IConfiguration _configuration;

        public InspectionPlanService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            string connectionString =
                _configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException(
                    "connString connection string not found.");

            return new SqlConnection(connectionString);
        }

        // =========================================================
        // PROCESS AREA
        // =========================================================
        public async Task<List<Insplan_ProcessAreaModel>> GetProcessAreasAsync(
            decimal companyId)
        {
            var list = new List<Insplan_ProcessAreaModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            // உங்கள் existing ProcessArea table/SP பெயர்
            // project-ல் ஏற்கனவே இருக்கும் SP-ஐ இங்கே பயன்படுத்தவும்.
            string query = "SELECT [ProcessAreaID], [processarea] FROM [Tbl_ProcessArea] WHERE deleted = 0 and CompanyID=@companyid ORDER BY [processareaid]";
            await using SqlCommand cmd =
                new SqlCommand(query, conn);

            //cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@companyid", companyId);

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Insplan_ProcessAreaModel
                {
                    Id = Convert.ToDecimal(reader["ProcessAreaID"]),
                    Name = reader["processarea"]?.ToString() ?? ""
                });
            }

            return list;
        }

        // =========================================================
        // EQUIPMENT
        // =========================================================
        // EQUIPMENT FIX
        public async Task<List<Insplan_EquipmentModel>> GetEquipmentsAsync(decimal processAreaId)
        {
            var list = new List<Insplan_EquipmentModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();
            string query = "SELECT EquAutoID, EqupID, EqupType FROM Tbl_EquipmentAsset WHERE ProcessAreaID = @ProcID AND deleted = 0";

            await using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", processAreaId);

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Insplan_EquipmentModel
                {
                    // EqupID-ஐ Id-ஆக மாற்றியுள்ளோம் (Query-ல் EquipID இல்லை, EqupID தான் உள்ளது)
                    Id = Convert.ToDecimal(reader["EquAutoID"]),
                    EquipType = reader["EqupID"]?.ToString() ?? ""
                });
            }

            return list;
        }

        // COMPONENT FIX
        public async Task<List<Insplan_ComponentModel>> GetComponentsAsync(decimal equipmentId)
        {
            var list = new List<Insplan_ComponentModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();
            string query = "SELECT compautoid, CompNo, compname FROM Tbl_EquipmentComponentDetails WHERE EqupID = @EquID AND deleted = 0";

            await using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@EquID", equipmentId.ToString());          

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new Insplan_ComponentModel
                {
                    Id = reader["compautoid"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["compautoid"]),

                    //CompNo = reader["CompNo"]?.ToString() ?? "",

                    Name = reader["compname"]?.ToString() ?? ""
                });
            }

           // return components;
            return list;
        }


        // =========================================================
        // SAVE / INSERT / UPDATE
        // Existing SP: sp_InspectEffectPlan_Save
        // =========================================================
        public async Task SaveInspectionPlanAsync(
            decimal processAreaId,
            decimal equipmentId,
            decimal componentId,
            string damageFact,
            string inspectCate,
            DateTime inspectDate,
            string saveFlag)
        {
            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand("sp_InspectEffectPlan_Save", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@ProcessareaIDp", processAreaId);

            cmd.Parameters.AddWithValue(
                "@EqupIDp", equipmentId);

            cmd.Parameters.AddWithValue(
                "@CompIDp", componentId);

            cmd.Parameters.AddWithValue(
                "@DamageFactp", damageFact ?? "");

            cmd.Parameters.AddWithValue(
                "@InspectCatep", inspectCate ?? "");

            cmd.Parameters.AddWithValue(
                "@InspectDatep", inspectDate);

            cmd.Parameters.AddWithValue(
                "@saveflagp", saveFlag ?? "N");

            await cmd.ExecuteNonQueryAsync();
        }

        // =========================================================
        // GET INSPECTION PLANS
        // =========================================================
        public async Task<List<InspectionPlanModel>>
            GetInspectionPlansAsync(
                decimal processAreaId,
                decimal equipmentId,
                decimal componentId)
        {
            var list = new List<InspectionPlanModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand(
                    @"SELECT
                        InspectID,
                        ProcID,
                        EquID,
                        CompID,
                        DamageFact,
                        InspectCate,
                        InspectDate
                      FROM InspectionPlan
                      WHERE ProcID = @ProcID
                        AND EquID = @EquID
                        AND CompID = @CompID
                      ORDER BY InspectID DESC",
                    conn);

            cmd.CommandType = CommandType.Text;

            cmd.Parameters.AddWithValue("@ProcID", processAreaId);
            cmd.Parameters.AddWithValue("@EquID", equipmentId);
            cmd.Parameters.AddWithValue("@CompID", componentId);

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new InspectionPlanModel
                {
                    InspectID = Convert.ToDecimal(
                        reader["InspectID"]),

                    ProcID = Convert.ToDecimal(
                        reader["ProcID"]),

                    EquID = Convert.ToDecimal(
                        reader["EquID"]),

                    CompID = Convert.ToDecimal(
                        reader["CompID"]),

                    DamageFact =
                        reader["DamageFact"]?.ToString() ?? "",

                    InspectCate =
                        reader["InspectCate"]?.ToString() ?? "",

                    InspectDate =
                        reader["InspectDate"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(
                                reader["InspectDate"])
                });
            }

            return list;
        }
    }

    // =============================================================
    // PROCESS AREA MODEL
    // =============================================================
    public class Insplan_ProcessAreaModel
    {
        public decimal Id { get; set; }

        public string Name { get; set; } = "";
    }

    // =============================================================
    // EQUIPMENT MODEL
    // =============================================================
    public class Insplan_EquipmentModel
    {
        public decimal Id { get; set; }

        public string EquipType { get; set; } = "";
    }

    // =============================================================
    // COMPONENT MODEL
    // =============================================================
    public class Insplan_ComponentModel
    {
        public decimal Id { get; set; }

        public string Name { get; set; } = "";
    }

    // =============================================================
    // INSPECTION PLAN MODEL
    // =============================================================
    public class InspectionPlanModel
    {
        public decimal InspectID { get; set; }

        public decimal ProcID { get; set; }

        public decimal EquID { get; set; }

        public decimal CompID { get; set; }

        public string DamageFact { get; set; } = "";

        public string InspectCate { get; set; } = "";

        public DateTime? InspectDate { get; set; }
    }
}