using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class SulfidationService
    {
        private readonly string _connectionString;

        public SulfidationService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // Fixed dropdown option tables
        // =================================================================

        public static readonly List<(string Label, string Code)> PHOptions = new()
        {
            ("< 5.5",      "5.5"),
            ("5.5 to 7.5", "7.5"),
            ("7.6 to 8.3", "7.6"),
            ("8.4 to 8.9", "8.4"),
            ("> 9.0",      "9.0"),
        };

        public static readonly List<(string Label, string Code)> H2SOptions = new()
        {
            ("< 55 ppm",         "Fiftyppm"),
            ("50 to 1000 ppm",   "Thosandppm"),
            ("1000 to 10000 ppm","Tenppm"),
            ("> 10000 ppm",      "GtTenppm"),
        };

        public static readonly List<(string Label, string Code)> HeatOptions = new()
        {
            ("As-Welded", "weld"),
            ("PWHT",      "pwht"),
        };

        public static readonly List<(string Label, string Code)> BrinnellOptions = new()
        {
            ("< 200",     "two"),
            ("200 - 237", "twothree"),
            ("> 237",     "gttwothree"),
        };

        // Svi text → numeric value (same lookup used in old calc)
        private static int SviToValue(string svi) => svi switch
        {
            "High"   => 100,
            "Medium" => 10,
            _        => 1,  // Low, None, anything else → 1
        };

        // =================================================================
        // 1. CASCADING DROPDOWNS
        // =================================================================

        public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(string companyId)
        {
            var list = new List<ProcessAreaModel>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT [ProcessAreaID],[processarea] FROM [Tbl_ProcessArea] WHERE deleted=0 AND CompanyID=@cid ORDER BY [processareaid]", conn);
            cmd.Parameters.Add("@cid", SqlDbType.Decimal).Value = companyId;
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (rd.HasRows && await rd.ReadAsync())
                list.Add(new ProcessAreaModel { ProcessAreaID = rd.GetDecimal(0), ProcessArea = rd.IsDBNull(1) ? "" : rd.GetString(1) });
            return list;
        }

        public async Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId)
        {
            var list = new List<EquipmentModel>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT EquAutoID,EqupID,EqupType FROM Tbl_EquipmentAsset WHERE ProcessAreaID=@pid AND deleted=0", conn);
            cmd.Parameters.Add("@pid", SqlDbType.Decimal).Value = processAreaId;
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (rd.HasRows && await rd.ReadAsync())
                list.Add(new EquipmentModel { EquAutoID = rd.GetDecimal(0), EquPID = $"{rd.GetString(1)} - {rd.GetString(2)}" });
            return list;
        }

        public async Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId)
        {
            var list = new List<ComponentModel>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT compautoid,CompNo,compname FROM Tbl_EquipmentComponentDetails WHERE EqupID=@eid AND deleted=0", conn);
            cmd.Parameters.Add("@eid", SqlDbType.NVarChar, 20).Value = equipmentId ?? "";
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (rd.HasRows && await rd.ReadAsync())
                list.Add(new ComponentModel { CompAutoID = rd.GetDecimal(0), CompNo = $"{rd.GetString(1)} - {rd.GetString(2)}" });
            return list;
        }

        // =================================================================
        // 2. EXISTING RECORD LOAD
        // =================================================================

        public async Task<SulfidationModel?> GetSulfidationRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM SulfideCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0", conn);
            cmd.Parameters.AddWithValue("@p", procId);
            cmd.Parameters.AddWithValue("@e", equId);
            cmd.Parameters.AddWithValue("@c", compId);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;

            decimal? Dec(string col) => rd[col] == DBNull.Value ? null : Convert.ToDecimal(rd[col]);
            int? Int(string col) => rd[col] == DBNull.Value ? null : Convert.ToInt32(rd[col]);
            string? S(string col) => rd[col] == DBNull.Value ? null : rd[col].ToString()?.Trim();
            DateTime? Dt(string col) => rd[col] == DBNull.Value ? null : Convert.ToDateTime(rd[col]);

            // Reverse-map stored labels back to codes for dropdowns
            string? phLabel  = S("pHwater");
            string? h2sLabel = S("H2S");
            string? heatLabel = S("Heat");
            string? brinLabel = S("Brinnell");

            return new SulfidationModel
            {
                SulID = rd.GetDecimal(rd.GetOrdinal("SulID")),
                ProcID = procId, EquID = equId, CompID = compId,
                SulAge = Int("SulAge"),
                SulInsEff = S("SulInsEff"),
                SulnofIns = Int("SulnofIns"),
                InspectDate = Dt("InspectDate"),
                PHwater = phLabel,
                PHwaterCode = PHOptions.Find(o => o.Label == phLabel).Code,
                H2S = h2sLabel,
                H2SCode = H2SOptions.Find(o => o.Label == h2sLabel).Code,
                Severity = S("Severity"),
                Heat = heatLabel,
                HeatCode = HeatOptions.Find(o => o.Label == heatLabel).Code,
                Brinnell = brinLabel,
                BrinnellCode = BrinnellOptions.Find(o => o.Label == brinLabel).Code,
                SulSvi = S("SulSvi"),
                SulSviVal = Int("SulSviVal"),
                SulDf = Dec("SulDf"),
            };
        }

        // =================================================================
        // 3. STEP 1 — pH + H2S → Environmental Severity  (Ref_Envi_Sev)
        //    Column selected = H2SCode (Fiftyppm / Thosandppm / Tenppm / GtTenppm)
        //    Row matched by pHofWater numeric value
        // =================================================================

        public async Task<string> ComputeEnvSeverityAsync(string h2sCode, string phCode)
        {
            string col = h2sCode switch
            {
                "Fiftyppm"   => "Fiftyppm",
                "Thosandppm" => "Thosandppm",
                "Tenppm"     => "Tenppm",
                "GtTenppm"   => "GtTenppm",
                _ => throw new InvalidOperationException($"Invalid H2S code: {h2sCode}")
            };

            if (!decimal.TryParse(phCode, out decimal phVal))
                throw new InvalidOperationException($"Invalid pH code: {phCode}");

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand($"SELECT {col} AS sev FROM Ref_Envi_Sev WHERE pHofWater=@ph", conn);
            cmd.Parameters.AddWithValue("@ph", phVal);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("No matching Environmental Severity found in Ref_Envi_Sev.");
            return result.ToString()?.Trim() ?? "";
        }

        // =================================================================
        // 4. STEP 2 — Env Severity + Heat + Brinnell → Svi  (Ref_EnviSul)
        //    Column selected = BrinnellCode (two / twothree / gttwothree)
        //    Row matched by Heat code and Envi (severity text)
        // =================================================================

        public async Task<string> ComputeSviAsync(string brinnellCode, string heatCode, string severity)
        {
            string col = brinnellCode switch
            {
                "two"        => "two",
                "twothree"   => "twothree",
                "gttwothree" => "gttwothree",
                _ => throw new InvalidOperationException($"Invalid Brinnell code: {brinnellCode}")
            };

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                $"SELECT {col} AS svi FROM Ref_EnviSul WHERE Heat=@heat AND Envi=@envi", conn);
            cmd.Parameters.AddWithValue("@heat", heatCode);
            cmd.Parameters.AddWithValue("@envi", severity);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("No matching Svi found in Ref_EnviSul.");
            return result.ToString()?.Trim() ?? "";
        }

        // =================================================================
        // 5. CALCULATE (no DB write)
        //    Formula = Ref_SCC[InsEff column, Svi numeric, nofins] × Age^1.1
        // =================================================================

        public async Task<SulfidationModel> CalculateSulfidationAsync(SulfidationModel m)
        {
            if (string.IsNullOrEmpty(m.SulInsEff)) throw new InvalidOperationException("Inspection Effectiveness is required.");
            if (string.IsNullOrEmpty(m.SulSvi))    throw new InvalidOperationException("Svi is required. Select pH, H2S, Heat and Brinnell first.");
            if (m.SulnofIns == null)               throw new InvalidOperationException("No. of Inspections is required.");
            if (m.SulAge == null || m.SulAge <= 0) throw new InvalidOperationException("Age is required and must be > 0.");

            int sviVal = SviToValue(m.SulSvi);
            m.SulSviVal = sviVal;

            string col = m.SulInsEff switch
            {
                "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E",
                _ => throw new InvalidOperationException($"Invalid Inspection Effectiveness: {m.SulInsEff}")
            };

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                $"SELECT {col} AS dfSul FROM Ref_SCC WHERE Svi=@svi AND Inspection=@nofins", conn);
            cmd.Parameters.AddWithValue("@svi", sviVal);
            cmd.Parameters.AddWithValue("@nofins", m.SulnofIns.Value);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();

            if (!await rd.ReadAsync() || rd["dfSul"] == DBNull.Value)
                throw new InvalidOperationException("No matching record found in Ref_SCC for the selected Svi and No. of Inspections.");

            double dfSul = Convert.ToDouble(rd["dfSul"]);
            double dfb = dfSul * Math.Pow(m.SulAge.Value, 1.1);
            m.SulDf = Convert.ToDecimal(Math.Round(dfb, 3));
            return m;
        }

        // =================================================================
        // 6. SAVE (separate)
        // =================================================================

        public async Task<SulfidationModel> SaveSulfidationAsync(SulfidationModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int count = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(1) FROM SulfideCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0",
                conn) { Parameters = { new("@p", m.ProcID ?? 0), new("@e", m.EquID ?? 0), new("@c", m.CompID ?? 0) } }
                .ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE SulfideCracking SET
SulAge=@SulAge, SulInsEff=@SulInsEff, SulnofIns=@SulnofIns, InspectDate=@InspectDate,
pHwater=@pHwater, H2S=@H2S, Severity=@Severity, Heat=@Heat, Brinnell=@Brinnell,
SulSvi=@SulSvi, SulSviVal=@SulSviVal, SulDf=@SulDf,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO SulfideCracking
(ProcID, EquID, CompID, SulAge, SulInsEff, SulnofIns, InspectDate,
 pHwater, H2S, Severity, Heat, Brinnell, SulSvi, SulSviVal, SulDf,
 Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @SulAge, @SulInsEff, @SulnofIns, @InspectDate,
 @pHwater, @H2S, @Severity, @Heat, @Brinnell, @SulSvi, @SulSviVal, @SulDf,
 0, @CreatedBy, @CreatedDate)";
            }

            string LabelOf(List<(string Label, string Code)> opts, string? code) =>
                opts.Find(o => o.Code == code).Label ?? code ?? "";

            cmd.Parameters.AddWithValue("@ProcID",      m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID",       m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID",      m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@SulAge",      (object?)m.SulAge ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SulInsEff",   (object?)m.SulInsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SulnofIns",   (object?)m.SulnofIns ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pHwater",     LabelOf(PHOptions,       m.PHwaterCode));
            cmd.Parameters.AddWithValue("@H2S",         LabelOf(H2SOptions,      m.H2SCode));
            cmd.Parameters.AddWithValue("@Severity",    (object?)m.Severity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Heat",        LabelOf(HeatOptions,     m.HeatCode));
            cmd.Parameters.AddWithValue("@Brinnell",    LabelOf(BrinnellOptions, m.BrinnellCode));
            cmd.Parameters.AddWithValue("@SulSvi",      (object?)m.SulSvi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SulSviVal",   (object?)m.SulSviVal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SulDf",       (object?)m.SulDf ?? DBNull.Value);

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy",   (object?)m.UpdatedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy",  (object?)m.CreatedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
            return m;
        }

        public async Task<bool> DeleteSulfidationAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM SulfideCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c", conn);
            cmd.Parameters.AddWithValue("@p", procId ?? 0);
            cmd.Parameters.AddWithValue("@e", equId ?? 0);
            cmd.Parameters.AddWithValue("@c", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
