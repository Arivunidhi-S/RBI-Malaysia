using System.Data;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services
{
    public class POFExternalCLSCCService
    {
        private readonly IConfiguration _configuration;

        public POFExternalCLSCCService(
            IConfiguration configuration)
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

        public async Task<List<ProcessAreaModel>> GetProcessAreasAsync()
        {
            var list = new List<ProcessAreaModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            using SqlCommand cmd = new SqlCommand(
                @"SELECT ProcessAreaID, ProcessArea
          FROM Tbl_ProcessArea
          WHERE Deleted = 0
          ORDER BY ProcessArea", conn);

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ProcessAreaModel
                {
                    ProcessAreaID = Convert.ToDecimal(reader["ProcessAreaID"]),
                    ProcessArea = reader["ProcessArea"]?.ToString() ?? ""
                });
            }

            return list;
        }
        public async Task<List<EquipmentModel>> GetEquipmentsAsync(decimal processAreaId)
        {
            var list = new List<EquipmentModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            using SqlCommand cmd = new SqlCommand(
                @"SELECT EquAutoID, EqupType, EqupID
          FROM Tbl_EquipmentAsset
          WHERE ProcessAreaID = @ProcessAreaID
            AND Deleted = 0
          ORDER BY EqupID", conn);

            cmd.Parameters.Add("@ProcessAreaID", SqlDbType.Decimal)
               .Value = processAreaId;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EquipmentModel
                {
                    EquAutoID = Convert.ToDecimal(reader["EquAutoID"]),
                    EquPID = reader["EqupID"]?.ToString() ?? "",
                    EqupType = reader["EqupType"]?.ToString() ?? ""
                });
            }

            return list;
        }

        public async Task<List<ComponentModel>> GetComponentsAsync(decimal equipmentId)
        {
            var list = new List<ComponentModel>();

            await using SqlConnection conn = GetConnection();
            await conn.OpenAsync();

            using SqlCommand cmd = new SqlCommand(
                @"SELECT compautoid,
                 CompNo,
                 CompName,
                 InspectionEffective,
                 NoofInspection,
                 OPTemp,
                 Clad,
                 MRT,
                 CorrosionAllownce
          FROM Tbl_EquipmentComponentDetails
          WHERE EqupID = @EqupID
            AND Deleted = 0
          ORDER BY CompNo", conn);

            cmd.Parameters.Add("@EqupID", SqlDbType.Decimal)
               .Value = equipmentId;

            await using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ComponentModel
                {
                    CompAutoID = Convert.ToDecimal(reader["compautoid"]),
                    CompNo = reader["CompNo"]?.ToString() ?? "",
                    CompName = reader["CompName"]?.ToString() ?? "",
                    InspectionEffective =
                        reader["InspectionEffective"]?.ToString() ?? "",
                    NoofInspection =
                        Convert.ToInt32(reader["NoofInspection"]),
                    OPTemp = reader["OPTemp"]?.ToString() ?? "",
                    Clad = reader["Clad"]?.ToString() ?? "",
                    MRT = Convert.ToInt32(reader["MRT"]),
                    CorrosionAllownce =
                        reader["CorrosionAllownce"]?.ToString() ?? ""
                });
            }

            return list;
        }
        // =========================================================
        // SAVE / UPDATE
        // =========================================================

        public async Task SaveAsync(
            POFExternalCLSCCModel model,
            decimal userId,
            string saveFlag)
        {
            await using SqlConnection conn = GetConnection();

            await conn.OpenAsync();

            await using SqlCommand cmd =
                new SqlCommand(
                    "sp_ExCLS_Save",
                    conn);

            cmd.CommandType =
                CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue(
                "@ProcessareaIDp",
                model.ProcID);

            cmd.Parameters.AddWithValue(
                "@EqupIDp",
                model.EquID);

            cmd.Parameters.AddWithValue(
                "@CompIDp",
                model.CompID);

            cmd.Parameters.AddWithValue(
                "@agetkp",
                model.Agtk);

            cmd.Parameters.AddWithValue(
                "@InsEffp",
                model.InsEff ?? "");

            cmd.Parameters.AddWithValue(
                "@compDtp",
                model.CmpDt ?? DateTime.Now);

            cmd.Parameters.AddWithValue(
                "@calDtp",
                model.CalDt ?? DateTime.Now);

            cmd.Parameters.AddWithValue(
                "@NofInsp",
                model.NofIns ?? 0);

            cmd.Parameters.AddWithValue(
                "@InspectDate",
                model.InspectDate ?? DateTime.Now);

            cmd.Parameters.AddWithValue(
                "@coatqualp",
                model.CoatQual ?? "");

            cmd.Parameters.AddWithValue(
                "@agep",
                model.Age);

            cmd.Parameters.AddWithValue(
                "@CrDriverp",
                model.CrDriver ?? "");

            cmd.Parameters.AddWithValue(
                "@svip",
                model.Svi ?? "");

            cmd.Parameters.AddWithValue(
                "@Dfexclsp",
                model.ExCLSDf);

            cmd.Parameters.AddWithValue(
                "@CompanyID",
                model.CompanyID);

            cmd.Parameters.AddWithValue(
                "@useridp",
                userId);

            cmd.Parameters.AddWithValue(
                "@saveflagp",
                saveFlag);

            cmd.Parameters.AddWithValue(
                "@idp",
                model.ExCLSID);

            await cmd.ExecuteNonQueryAsync();
        }


        // =========================================================
        // GET SVI
        // Legacy:
        // Ref_ExCLSCC WHERE opTemp = calculated temperature
        // =========================================================

        public async Task<string> GetSviAsync(
            string area,
            decimal operatingTemperature)
        {
            decimal temp;

            if (operatingTemperature < 49)
                temp = 49;
            else if (operatingTemperature >= 49 &&
                     operatingTemperature < 93)
                temp = 93;
            else if (operatingTemperature >= 93 &&
                     operatingTemperature < 149)
                temp = 149;
            else
                temp = 150;


            string columnName = area switch
            {
                "marine" => "marine",
                "temp" => "temp",
                "arid" => "arid",
                "severe" => "severe",
                _ => throw new ArgumentException(
                    "Invalid corrosion area.")
            };


            string sql =
                $"""
                SELECT [{columnName}]
                FROM Ref_ExCLSCC
                WHERE opTemp = @opTemp
                """;


            await using SqlConnection conn = GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "@opTemp",
                temp);

            await conn.OpenAsync();

            object? value =
                await cmd.ExecuteScalarAsync();

            return value == null ||
                   value == DBNull.Value
                ? ""
                : value.ToString() ?? "";
        }


        // =========================================================
        // GET Df-ext-CLSCC
        // Legacy:
        //
        // SVI High   = 50
        // SVI Medium = 10
        // SVI Low    = 1
        // SVI None   = 1
        //
        // df = Ref_SCC[InspectionEffectiveness]
        // ExCLSDf = df * Age ^ 1.1
        // =========================================================

        public async Task<decimal> CalculateDfAsync(
            string inspectionEffectiveness,
            int noOfInspection,
            string svi,
            decimal age)
        {
            int sviValue =
                svi switch
                {
                    "High" => 50,
                    "Medium" => 10,
                    "Low" => 1,
                    "None" => 1,
                    _ => 1
                };


            string columnName =
                inspectionEffectiveness switch
                {
                    "A" => "A",
                    "B" => "B",
                    "C" => "C",
                    "D" => "D",
                    "E" => "E",
                    _ => throw new ArgumentException(
                        "Invalid Inspection Effectiveness.")
                };


            string sql =
                $"""
                SELECT [{columnName}]
                FROM Ref_SCC
                WHERE Inspection = @Inspection
                  AND Svi = @Svi
                """;


            await using SqlConnection conn = GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "@Inspection",
                noOfInspection);

            cmd.Parameters.AddWithValue(
                "@Svi",
                sviValue);

            await conn.OpenAsync();

            object? result =
                await cmd.ExecuteScalarAsync();

            if (result == null ||
                result == DBNull.Value)
            {
                return 0;
            }


            decimal df =
                Convert.ToDecimal(result);


            decimal finalValue =
                df * (decimal)Math.Pow(
                    (double)age,
                    1.1);


            return Math.Round(
                finalValue,
                3);
        }


        // =========================================================
        // COATING AGE
        // Legacy:
        //
        // 1 = Installation date
        // 2 = Installation date + 5 years
        // 3 = Installation date + 15 years
        //
        // Age = Max(0, days / 365)
        // =========================================================

        public decimal CalculateCoatingAge(
            string coatingQuality,
            DateTime installationDate,
            DateTime calculationDate)
        {
            DateTime startDate;

            switch (coatingQuality)
            {
                case "1":
                    startDate =
                        installationDate;
                    break;

                case "2":
                    startDate =
                        installationDate.AddYears(5);
                    break;

                case "3":
                    startDate =
                        installationDate.AddYears(15);
                    break;

                default:
                    startDate =
                        installationDate;
                    break;
            }


            TimeSpan difference =
                calculationDate - startDate;


            int age =
                Convert.ToInt32(
                    difference.TotalDays) / 365;


            return Math.Max(0, age);
        }


        // =========================================================
        // GET RECORD BY ID
        // =========================================================

        public async Task<POFExternalCLSCCModel?>
            GetByIdAsync(
                decimal id,
                decimal companyId)
        {
            const string sql =
                """
                SELECT
                    ExCLSID,
                    ProcID,
                    EquID,
                    CompID,
                    Agtk,
                    InsEff,
                    cmpdt,
                    caldt,
                    nofins,
                    InspectDate,
                    coatqual,
                    age,
                    crdriver,
                    Svi,
                    ExCLSDf,
                    CompanyID,
                    CreatedBy,
                    CreatedDate,
                    ModifiedBy,
                    ModifiedDate,
                    Rowversions,
                    Deleted
                FROM dbo.ExternalCLSCC
                WHERE ExCLSID = @id
                  AND CompanyID = @CompanyID
                  AND ISNULL(Deleted,0) = 0
                """;


            await using SqlConnection conn =
                GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "@id",
                id);

            cmd.Parameters.AddWithValue(
                "@CompanyID",
                companyId);

            await conn.OpenAsync();

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;


            return Map(reader);
        }


        // =========================================================
        // LIST
        // =========================================================

        public async Task<List<POFExternalCLSCCModel>>
            GetListAsync(
                decimal companyId,
                decimal? procId = null,
                decimal? equId = null,
                decimal? compId = null)
        {
            const string sql =
                """
                SELECT
                    ExCLSID,
                    ProcID,
                    EquID,
                    CompID,
                    Agtk,
                    InsEff,
                    cmpdt,
                    caldt,
                    nofins,
                    InspectDate,
                    coatqual,
                    age,
                    crdriver,
                    Svi,
                    ExCLSDf,
                    CompanyID,
                    CreatedBy,
                    CreatedDate,
                    ModifiedBy,
                    ModifiedDate,
                    Rowversions,
                    Deleted
                FROM dbo.ExternalCLSCC
                WHERE CompanyID = @CompanyID
                  AND ISNULL(Deleted,0) = 0
                  AND (@ProcID IS NULL OR ProcID = @ProcID)
                  AND (@EquID IS NULL OR EquID = @EquID)
                  AND (@CompID IS NULL OR CompID = @CompID)
                ORDER BY ExCLSID DESC
                """;


            List<POFExternalCLSCCModel> result = new();


            await using SqlConnection conn =
                GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "@CompanyID",
                companyId);

            cmd.Parameters.AddWithValue(
                "@ProcID",
                procId.HasValue
                    ? procId.Value
                    : DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@EquID",
                equId.HasValue
                    ? equId.Value
                    : DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@CompID",
                compId.HasValue
                    ? compId.Value
                    : DBNull.Value);

            await conn.OpenAsync();

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(Map(reader));
            }

            return result;
        }


        // =========================================================
        // DELETE
        // =========================================================

        public async Task DeleteAsync(
            decimal id,
            decimal userId)
        {
            const string sql =
                """
                UPDATE dbo.ExternalCLSCC
                SET
                    Deleted = 1,
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE(),
                    Rowversions =
                        ISNULL(Rowversions,0) + 1
                WHERE ExCLSID = @ExCLSID
                """;


            await using SqlConnection conn =
                GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue(
                "@ExCLSID",
                id);

            cmd.Parameters.AddWithValue(
                "@ModifiedBy",
                userId);

            await conn.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }


        // =========================================================
        // MAP
        // =========================================================

        private static POFExternalCLSCCModel Map(
            SqlDataReader reader)
        {
            return new POFExternalCLSCCModel
            {
                ExCLSID =
                    Convert.ToDecimal(
                        reader["ExCLSID"]),

                ProcID =
                    Convert.ToDecimal(
                        reader["ProcID"]),

                EquID =
                    Convert.ToDecimal(
                        reader["EquID"]),

                CompID =
                    Convert.ToDecimal(
                        reader["CompID"]),

                Agtk =
                    reader["Agtk"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(
                            reader["Agtk"]),

                InsEff =
                    reader["InsEff"]?.ToString()
                    ?? "",

                CmpDt =
                    reader["cmpdt"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["cmpdt"]),

                CalDt =
                    reader["caldt"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["caldt"]),

                NofIns =
                    reader["nofins"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(
                            reader["nofins"]),

                InspectDate =
                    reader["InspectDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["InspectDate"]),

                CoatQual =
                    reader["coatqual"]?.ToString()
                    ?? "",

                Age =
                    reader["age"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(
                            reader["age"]),

                CrDriver =
                    reader["crdriver"]?.ToString()
                    ?? "",

                Svi =
                    reader["Svi"]?.ToString()
                    ?? "",

                ExCLSDf =
                    reader["ExCLSDf"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(
                            reader["ExCLSDf"]),

                CompanyID =
                    reader["CompanyID"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(
                            reader["CompanyID"])
            };
        }
    }
}

