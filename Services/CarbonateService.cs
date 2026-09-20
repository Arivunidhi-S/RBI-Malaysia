using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class CarbonateService
    {
        private readonly string _connectionString;

        public CarbonateService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // Fixed dropdown option tables (exact match to old ASP.Net markup)
        // =================================================================

        public static readonly List<(string Label, string Code)> PHOptions = new()
        {
            ("7.6 to 8.3",    "7.6"),
            ("> 8.3 to < 9.0","8.3"),
            ("=> 9.0",        "9.0"),
        };

        public static readonly List<(string Label, string Code)> CO3Options = new()
        {
            ("< 100 ppm",       "Hundredppm"),
            ("100 to 500 ppm",  "Five100ppm"),
            ("500 to 1000 ppm", "Thousandppm"),
            ("> 1000 ppm",      "GtThousandppm"),
        };

        private static int SviToValue(string svi) => svi switch
        {
            "High"   => 1000,
            "Medium" => 100,
            "Low"    => 10,
            _        => 1,   // None
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

        public async Task<CarbonateModel?> GetCarbonateRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM CarbonateCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0", conn);
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

            string? phLabel  = S("pHwater");
            string? co3Label = S("CO3");

            return new CarbonateModel
            {
                CO3ID = rd.GetDecimal(rd.GetOrdinal("CO3ID")),
                ProcID = procId, EquID = equId, CompID = compId,
                CO3Age = Int("CO3Age"),
                CO3InsEff = S("CO3InsEff"),
                CO3nofIns = Int("CO3nofIns"),
                InspectDate = Dt("InspectDate"),
                PHwater = phLabel,
                PHwaterCode = PHOptions.Find(o => o.Label == phLabel).Code,
                CO3 = co3Label,
                CO3Code = CO3Options.Find(o => o.Label == co3Label).Code,
                CO3Svi = S("CO3Svi"),
                CO3SviVal = Int("CO3SviVal"),
                CO3Df = Dec("CO3Df"),
            };
        }

        // =================================================================
        // 3. STEP 1 — pH + CO3 → Svi text   (Ref_CO3)
        // =================================================================

        public async Task<string> ComputeSviAsync(string co3Code, string phCode)
        {
            string col = co3Code switch
            {
                "Hundredppm"    => "Hundredppm",
                "Five100ppm"    => "Five100ppm",
                "Thousandppm"   => "Thousandppm",
                "GtThousandppm" => "GtThousandppm",
                _ => throw new InvalidOperationException($"Invalid CO3 code: {co3Code}")
            };

            if (!decimal.TryParse(phCode, out decimal phVal))
                throw new InvalidOperationException($"Invalid pH code: {phCode}");

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand($"SELECT {col} AS svi FROM Ref_CO3 WHERE pHofWater=@ph", conn);
            cmd.Parameters.AddWithValue("@ph", phVal);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException("No matching Svi found in Ref_CO3 for the selected pH and CO3 values.");
            return result.ToString()?.Trim() ?? "";
        }

        // =================================================================
        // 4. CALCULATE (no DB write)
        //    Formula: Ref_SCC[InsEff, SviVal, nofins] × Age^1.1
        // =================================================================

        public async Task<CarbonateModel> CalculateCarbonateAsync(CarbonateModel m)
        {
            if (string.IsNullOrEmpty(m.CO3InsEff))    throw new InvalidOperationException("Inspection Effectiveness is required.");
            if (string.IsNullOrEmpty(m.CO3Svi))       throw new InvalidOperationException("Svi is required. Select pH and CO3 first.");
            if (m.CO3nofIns == null)                   throw new InvalidOperationException("No. of Inspections is required.");
            if (m.CO3Age == null || m.CO3Age <= 0)     throw new InvalidOperationException("Age is required and must be > 0.");

            int sviVal = SviToValue(m.CO3Svi);
            m.CO3SviVal = sviVal;

            string col = m.CO3InsEff switch
            {
                "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E",
                _ => throw new InvalidOperationException($"Invalid Inspection Effectiveness: {m.CO3InsEff}")
            };

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                $"SELECT {col} AS dfcrbnt FROM Ref_SCC WHERE Inspection=@nofins AND Svi=@svi", conn);
            cmd.Parameters.AddWithValue("@nofins", m.CO3nofIns.Value);
            cmd.Parameters.AddWithValue("@svi", sviVal);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();

            if (!await rd.ReadAsync() || rd["dfcrbnt"] == DBNull.Value)
                throw new InvalidOperationException("No matching record in Ref_SCC for the selected values.");

            double df = Convert.ToDouble(rd["dfcrbnt"]);
            double dfb = df * Math.Pow(m.CO3Age.Value, 1.1);
            m.CO3Df = Convert.ToDecimal(Math.Round(dfb, 3));
            return m;
        }

        // =================================================================
        // 5. SAVE (separate)
        // =================================================================

        public async Task<CarbonateModel> SaveCarbonateAsync(CarbonateModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int count = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(1) FROM CarbonateCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0",
                conn) { Parameters = { new("@p", m.ProcID ?? 0), new("@e", m.EquID ?? 0), new("@c", m.CompID ?? 0) } }
                .ExecuteScalarAsync());

            string LabelOf(List<(string Label, string Code)> opts, string? code) =>
                opts.Find(o => o.Code == code).Label ?? code ?? "";

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE CarbonateCracking SET
CO3Age=@CO3Age, CO3InsEff=@CO3InsEff, CO3nofIns=@CO3nofIns, InspectDate=@InspectDate,
pHwater=@pHwater, CO3=@CO3, CO3Svi=@CO3Svi, CO3SviVal=@CO3SviVal, CO3Df=@CO3Df,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO CarbonateCracking
(ProcID, EquID, CompID, CO3Age, CO3InsEff, CO3nofIns, InspectDate,
 pHwater, CO3, CO3Svi, CO3SviVal, CO3Df, Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @CO3Age, @CO3InsEff, @CO3nofIns, @InspectDate,
 @pHwater, @CO3, @CO3Svi, @CO3SviVal, @CO3Df, 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID",      m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID",       m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID",      m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@CO3Age",      (object?)m.CO3Age ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CO3InsEff",   (object?)m.CO3InsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CO3nofIns",   (object?)m.CO3nofIns ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pHwater",     LabelOf(PHOptions,  m.PHwaterCode));
            cmd.Parameters.AddWithValue("@CO3",         LabelOf(CO3Options, m.CO3Code));
            cmd.Parameters.AddWithValue("@CO3Svi",      (object?)m.CO3Svi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CO3SviVal",   (object?)m.CO3SviVal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CO3Df",       (object?)m.CO3Df ?? DBNull.Value);

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

        public async Task<bool> DeleteCarbonateAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM CarbonateCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c", conn);
            cmd.Parameters.AddWithValue("@p", procId ?? 0);
            cmd.Parameters.AddWithValue("@e", equId ?? 0);
            cmd.Parameters.AddWithValue("@c", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
