using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class CUIService
    {
        private readonly string _connectionString;

        public CUIService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // Fixed dropdown option tables (values match the old ASP.Net markup exactly)
        // =================================================================

        // Coating Quality: code -> label persisted in the `coatqual` column
        private static readonly Dictionary<string, string> CoatQualLabels = new()
        {
            ["1"] = "No Coating or Poor Quality",
            ["2"] = "Medium Quality",
            ["3"] = "High Quality",
        };

        // Cr Driver: code (also the Ref_CUI column name) -> label persisted in the `crdriver` column
        private static readonly Dictionary<string, string> CrDriverLabels = new()
        {
            ["marine"] = "Marine/Cooling Tower Drift Area",
            ["temp"] = "Temperature",
            ["arid"] = "Arid/Dry",
            ["severe"] = "Severe",
        };

        // Fins: numeric value -> label persisted in the `fins` column
        // NOTE: Fiberglass/MineralWool/CalciumSilicate/Asbestos all carry the same numeric
        // value (1.25, differing only in trailing zeros in the old system) — preserved as-is.
        private static readonly List<(decimal Value, string Label)> FinsOptions = new()
        {
            (1m, "None"), (0.75m, "Foamglass"), (1.0m, "Pearlite"), (1.25m, "Fiberglass"),
            (1.250m, "MineralWool"), (1.2500m, "CalciumSilicate"), (1.25000m, "Asbestos"),
        };

        private static readonly List<(decimal Value, string Label)> FcmOptions = new()
        {
            (0.75m, "Below Average"), (1.0m, "Average"), (1.25m, "Above Average"),
        };

        private static readonly List<(decimal Value, string Label)> FicOptions = new()
        {
            (1.25m, "Below Average"), (1.0m, "Average"), (0.75m, "Above Average"),
        };

        private static string LabelFor(List<(decimal Value, string Label)> options, decimal? value)
        {
            if (value == null) return "";
            foreach (var (v, label) in options)
                if (v == value.Value) return label;
            return value.Value.ToString();
        }

        // =================================================================
        // 1. CASCADING DROPDOWNS
        // =================================================================

        public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(string companyId)
        {
            var list = new List<ProcessAreaModel>();
            string query = "SELECT [ProcessAreaID], [processarea] FROM [Tbl_ProcessArea] WHERE deleted = 0 and CompanyID=@companyid ORDER BY [processareaid]";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@companyid", SqlDbType.Decimal).Value = companyId;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new ProcessAreaModel
                {
                    ProcessAreaID = reader.GetDecimal(0),
                    ProcessArea = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
            return list;
        }

        public async Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId)
        {
            var list = new List<EquipmentModel>();
            string query = "SELECT EquAutoID, EqupID, EqupType FROM Tbl_EquipmentAsset WHERE ProcessAreaID = @ProcID AND deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@ProcID", SqlDbType.Decimal).Value = processAreaId;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new EquipmentModel
                {
                    EquAutoID = reader.GetDecimal(0),
                    EquPID = $"{reader.GetString(1)} - {reader.GetString(2)}"
                });
            }
            return list;
        }

        public async Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId)
        {
            var list = new List<ComponentModel>();
            string query = "SELECT compautoid, CompNo, compname FROM Tbl_EquipmentComponentDetails WHERE EqupID = @EquID AND deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@EquID", SqlDbType.NVarChar, 20).Value = equipmentId ?? "";
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new ComponentModel
                {
                    CompAutoID = reader.GetDecimal(0),
                    CompNo = $"{reader.GetString(1)} - {reader.GetString(2)}"
                });
            }
            return list;
        }

        // =================================================================
        // 2. EXISTING RECORD LOAD
        // =================================================================

        public async Task<CUIModel?> GetCUIRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT * FROM CUI WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID AND Deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            decimal? Dec(string col) => reader[col] == DBNull.Value ? null : Convert.ToDecimal(reader[col]);
            int? Int(string col) => reader[col] == DBNull.Value ? null : Convert.ToInt32(reader[col]);
            string? S(string col) => reader[col] == DBNull.Value ? null : reader[col].ToString();
            DateTime? Dt(string col) => reader[col] == DBNull.Value ? null : Convert.ToDateTime(reader[col]);

            string? coatLabel = S("coatqual");
            string? coatCode = null;
            foreach (var kv in CoatQualLabels) if (kv.Value == coatLabel) coatCode = kv.Key;

            string? crLabel = S("crdriver");
            string? crCode = null;
            foreach (var kv in CrDriverLabels) if (kv.Value == crLabel) crCode = kv.Key;

            decimal? FindValue(List<(decimal Value, string Label)> options, string? label)
            {
                foreach (var (v, l) in options) if (l == label) return v;
                return null;
            }

            return new CUIModel
            {
                CUIID = reader.GetDecimal(reader.GetOrdinal("CUIID")),
                ProcID = procId,
                EquID = equId,
                CompID = compId,
                Agtk = Int("Agtk"),
                InsEff = S("InsEff"),
                CmpInstalDate = Dt("cmpdt"),
                CalcDate = Dt("caldt"),
                Nofins = Int("nofins"),
                InspectDate = Dt("InspectDate"),
                CoatQualCode = coatCode,
                CUIagcoat = Dec("CUIagcoat"),
                CUIage = Dec("CUIage"),
                Fps = Int("fps"),
                Fip = Int("fip"),
                CrDriverCode = crCode,
                Cr = Dec("cr"),
                CUIart = Dec("CUIart"),
                CUIDf = Dec("CUIDf"),
                Fins = FindValue(FinsOptions, S("fins")),
                Fcm = FindValue(FcmOptions, S("fcm")),
                Fic = FindValue(FicOptions, S("fic")),
                CompanyID = Dec("CompanyID"),
            };
        }

        // =================================================================
        // 3. COMPONENT DATA (MRT, CorrosionAllownce, ReadVal, OPTemp)
        // =================================================================

        public async Task<(double tmin, double ca, double trd, double opTemp)?> GetComponentDataAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT [MRT],[CorrosionAllownce],[ReadVal],[OPTemp] FROM [Tbl_EquipmentComponentDetails] " +
                            "WHERE [ProcessareaID]=@ProcID AND EqupID=@EquID AND [CompAutoID]=@CompID AND deleted=0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            double SafeD(object v) => double.TryParse(v?.ToString(), out var r) ? r : 0;
            return (SafeD(reader["MRT"]), SafeD(reader["CorrosionAllownce"]), SafeD(reader["ReadVal"]), SafeD(reader["OPTemp"]));
        }

        // =================================================================
        // 4. AUTO-CALC HANDLERS (fired on field change, before the main Calculate)
        // =================================================================

        /// <summary>Coating Quality change -> CUIagcoat (age since coating) & CUIage (min of Agtk, agecoat).</summary>
        public (decimal agecoat, decimal age) ComputeAge(string coatQualCode, DateTime cmpInstalDate, DateTime calcDate, int agtk)
        {
            DateTime dt = coatQualCode switch
            {
                "1" => cmpInstalDate,
                "2" => cmpInstalDate.AddYears(5),
                "3" => cmpInstalDate.AddYears(15),
                _ => cmpInstalDate,
            };
            int calagecoat = (int)((calcDate - dt).TotalDays) / 365;
            int agecoat = Math.Max(0, calagecoat);
            int age = Math.Min(agtk, agecoat);
            return (agecoat, age);
        }

        /// <summary>Cr Driver / Fins / Fcm / Fic / Fps / Fip change -> corrosion rate (cr).</summary>
        public async Task<decimal> ComputeCrAsync(string crDriverCode, double opTemp, decimal fins, decimal fcm, decimal fic, int fps, int fip)
        {
            double bucketed = BucketOpTemp(opTemp);

            string col = crDriverCode switch { "marine" => "marine", "temp" => "temp", "arid" => "arid", "severe" => "severe", _ => "marine" };
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand($"SELECT {col} AS crbCUI FROM Ref_CUI WHERE opTemp=@opTemp", conn);
            cmd.Parameters.AddWithValue("@opTemp", bucketed);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            double crb = result != null && result != DBNull.Value ? Convert.ToDouble(result) : 0;

            double cr = crb * (double)fins * (double)fcm * (double)fic * Math.Max(fps, fip);
            return Convert.ToDecimal(cr);
        }

        private static double BucketOpTemp(double optemp)
        {
            if (optemp > -12 && optemp < -8) return -12;
            if (optemp >= -8 && optemp < 6) return -8;
            if (optemp >= 6 && optemp < 32) return 6;
            if (optemp >= 32 && optemp < 71) return 32;
            if (optemp >= 71 && optemp < 107) return 71;
            if (optemp >= 107 && optemp < 135) return 107;
            if (optemp >= 135 && optemp < 162) return 135;
            if (optemp >= 162 && optemp < 176) return 162;
            if (optemp >= 176) return 176;
            return -12;
        }

        private static double BucketArt(double artval)
        {
            (double lo, double hi, double bucket)[] bands =
            {
                (0.02,0.04,0.02),(0.04,0.06,0.04),(0.06,0.08,0.06),(0.08,0.10,0.08),(0.10,0.12,0.10),
                (0.12,0.14,0.12),(0.14,0.16,0.14),(0.16,0.18,0.16),(0.18,0.20,0.18),(0.20,0.25,0.20),
                (0.25,0.30,0.25),(0.30,0.35,0.30),(0.35,0.40,0.40),(0.40,0.45,0.40),(0.45,0.50,0.45),
                (0.50,0.55,0.50),(0.55,0.60,0.55),(0.60,0.65,0.60),
            };
            foreach (var (lo, hi, bucket) in bands)
                if (artval >= lo && artval < hi) return bucket;
            if (artval >= 0.65) return 0.65;
            return 0.02; // matches old code's fallback for artval < 0.02
        }

        // =================================================================
        // 5. CALCULATE (no DB write)
        // =================================================================

        public async Task<CUIModel> CalculateCUIAsync(CUIModel m)
        {
            var data = await GetComponentDataAsync(m.ProcID ?? 0, m.EquID ?? 0, m.CompID ?? 0);
            if (data == null)
                throw new InvalidOperationException("Component data not found (Tbl_EquipmentComponentDetails).");

            var (tmin, ca, trd, opTemp) = data.Value;

            double cr = Convert.ToDouble(m.Cr ?? 0);
            double age = Convert.ToDouble(m.CUIage ?? 0);

            double ar = 1 - (trd - cr * age) / SafeDiv1(tmin + ca);
            double artval = Math.Max(ar, 0.0);
            m.CUIart = Convert.ToDecimal(artval);

            double finalArt = BucketArt(artval);

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            string col = m.InsEff switch { "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E", _ => "A" };
            using var cmd = new SqlCommand($"SELECT {col} AS CUI_inspect FROM Tbl_InspectionEffective WHERE art=@art AND inspection=@noins", conn);
            cmd.Parameters.AddWithValue("@art", finalArt);
            cmd.Parameters.AddWithValue("@noins", m.Nofins ?? 0);
            using var rd = await cmd.ExecuteReaderAsync();
            double df = 0;
            if (await rd.ReadAsync() && rd["CUI_inspect"] != DBNull.Value) df = Convert.ToDouble(rd["CUI_inspect"]);
            m.CUIDf = Convert.ToDecimal(df);

            return m;
        }

        private static double SafeDiv1(double v) => v == 0 ? 1 : v;

        // =================================================================
        // 6. SAVE (separate — persists whatever is currently in the model)
        // =================================================================

        public async Task<CUIModel> SaveCUIAsync(CUIModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string checkQuery = "SELECT COUNT(1) FROM CUI WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            using var checkCmd = new SqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            checkCmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            checkCmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE CUI SET
Agtk=@Agtk, InsEff=@InsEff, cmpdt=@cmpdt, caldt=@caldt, nofins=@nofins, InspectDate=@InspectDate,
coatqual=@coatqual, CUIagcoat=@CUIagcoat, CUIage=@CUIage, fps=@fps, fip=@fip, crdriver=@crdriver,
cr=@cr, CUIart=@CUIart, CUIDf=@CUIDf, fins=@fins, fcm=@fcm, fic=@fic, CompanyID=@CompanyID,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO CUI
(ProcID, EquID, CompID, Agtk, InsEff, cmpdt, caldt, nofins, InspectDate, coatqual, CUIagcoat, CUIage,
 fps, fip, crdriver, cr, CUIart, CUIDf, fins, fcm, fic, CompanyID, Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @Agtk, @InsEff, @cmpdt, @caldt, @nofins, @InspectDate, @coatqual, @CUIagcoat, @CUIage,
 @fps, @fip, @crdriver, @cr, @CUIart, @CUIDf, @fins, @fcm, @fic, @CompanyID, 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@Agtk", (object?)m.Agtk ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InsEff", (object?)m.InsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cmpdt", (object?)m.CmpInstalDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@caldt", (object?)m.CalcDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofins", (object?)m.Nofins ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@coatqual", m.CoatQualCode != null && CoatQualLabels.ContainsKey(m.CoatQualCode) ? CoatQualLabels[m.CoatQualCode] : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CUIagcoat", (object?)m.CUIagcoat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CUIage", (object?)m.CUIage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fps", (object?)m.Fps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fip", (object?)m.Fip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@crdriver", m.CrDriverCode != null && CrDriverLabels.ContainsKey(m.CrDriverCode) ? CrDriverLabels[m.CrDriverCode] : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@cr", (object?)m.Cr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CUIart", (object?)m.CUIart ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CUIDf", (object?)m.CUIDf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fins", LabelFor(FinsOptions, m.Fins));
            cmd.Parameters.AddWithValue("@fcm", LabelFor(FcmOptions, m.Fcm));
            cmd.Parameters.AddWithValue("@fic", LabelFor(FicOptions, m.Fic));
            cmd.Parameters.AddWithValue("@CompanyID", (object?)m.CompanyID ?? DBNull.Value);

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy", (object?)m.UpdatedBy ?? "1");
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)m.CreatedBy ?? "1");
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
            return m;
        }

        public async Task<bool> DeleteCUIAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            string query = "DELETE FROM CUI WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId ?? 0);
            cmd.Parameters.AddWithValue("@EquID", equId ?? 0);
            cmd.Parameters.AddWithValue("@CompID", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
