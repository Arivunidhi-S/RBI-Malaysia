using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class AmineService
    {
        private readonly string _connectionString;

        public AmineService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // Same Svi options as Caustic (old code uses same CausticAmineSave function with N1/U1 flag)
        public static readonly List<(string Label, int Value)> SviOptions = new()
        {
            ("High",   5000),
            ("Medium",  500),
            ("Low",      50),
            ("None",      1),
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

        public async Task<AmineModel?> GetAmineRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM AmineCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0", conn);
            cmd.Parameters.AddWithValue("@p", procId);
            cmd.Parameters.AddWithValue("@e", equId);
            cmd.Parameters.AddWithValue("@c", compId);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;

            decimal? Dec(string col) => rd[col] == DBNull.Value ? null : Convert.ToDecimal(rd[col]);
            int? Int(string col) => rd[col] == DBNull.Value ? null : Convert.ToInt32(rd[col]);
            string? S(string col) => rd[col] == DBNull.Value ? null : rd[col].ToString();
            DateTime? Dt(string col) => rd[col] == DBNull.Value ? null : Convert.ToDateTime(rd[col]);

            return new AmineModel
            {
                AmnID = rd.GetDecimal(rd.GetOrdinal("AmnID")),
                ProcID = procId, EquID = equId, CompID = compId,
                AmAge = Int("AmAge"),
                AmInsEff = S("AmInsEff"),
                AmnofIns = Int("AmnofIns"),
                InspectDate = Dt("InspectDate"),
                AmSvi = S("AmSvi"),
                AmSviVal = Int("AmSviVal"),
                AmDf = Dec("AmDf"),
            };
        }

        // =================================================================
        // 3. CALCULATE (no DB write)
        //    Formula identical to Caustic:
        //    dfAmn = Ref_SCC[InsEff column, Svi=AmSviVal, Inspection=AmnofIns]
        //    AmDf  = dfAmn × Age^1.1
        // =================================================================

        public async Task<AmineModel> CalculateAmineAsync(AmineModel m)
        {
            if (string.IsNullOrEmpty(m.AmInsEff))   throw new InvalidOperationException("Inspection Effectiveness is required.");
            if (m.AmSviVal == null)                  throw new InvalidOperationException("Susceptibility (Svi) is required.");
            if (m.AmnofIns == null)                  throw new InvalidOperationException("No. of Inspections is required.");
            if (m.AmAge == null || m.AmAge <= 0)     throw new InvalidOperationException("Age is required and must be > 0.");

            string col = m.AmInsEff switch
            {
                "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E",
                _ => throw new InvalidOperationException($"Invalid Inspection Effectiveness: {m.AmInsEff}")
            };

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                $"SELECT {col} AS dfAmn FROM Ref_SCC WHERE Svi=@svi AND Inspection=@nofins", conn);
            cmd.Parameters.AddWithValue("@svi", m.AmSviVal.Value);
            cmd.Parameters.AddWithValue("@nofins", m.AmnofIns.Value);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();

            if (!await rd.ReadAsync() || rd["dfAmn"] == DBNull.Value)
                throw new InvalidOperationException("No matching record found in Ref_SCC for the selected Svi and No. of Inspections.");

            double dfAmn = Convert.ToDouble(rd["dfAmn"]);
            double dfb = dfAmn * Math.Pow(m.AmAge.Value, 1.1);
            m.AmDf = Convert.ToDecimal(Math.Round(dfb, 3));
            return m;
        }

        // =================================================================
        // 4. SAVE (separate)
        // =================================================================

        public async Task<AmineModel> SaveAmineAsync(AmineModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int count = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(1) FROM AmineCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0",
                conn) { Parameters = { new("@p", m.ProcID ?? 0), new("@e", m.EquID ?? 0), new("@c", m.CompID ?? 0) } }
                .ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE AmineCracking SET
AmAge=@AmAge, AmInsEff=@AmInsEff, AmnofIns=@AmnofIns, InspectDate=@InspectDate,
AmSvi=@AmSvi, AmSviVal=@AmSviVal, AmDf=@AmDf,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO AmineCracking
(ProcID, EquID, CompID, AmAge, AmInsEff, AmnofIns, InspectDate, AmSvi, AmSviVal, AmDf,
 Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @AmAge, @AmInsEff, @AmnofIns, @InspectDate, @AmSvi, @AmSviVal, @AmDf,
 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID",      m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID",       m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID",      m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@AmAge",       (object?)m.AmAge ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AmInsEff",    (object?)m.AmInsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AmnofIns",    (object?)m.AmnofIns ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AmSvi",       (object?)m.AmSvi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AmSviVal",    (object?)m.AmSviVal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AmDf",        (object?)m.AmDf ?? DBNull.Value);

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy",   (object?)m.UpdatedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy",   (object?)m.CreatedBy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
            return m;
        }

        public async Task<bool> DeleteAmineAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM AmineCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c", conn);
            cmd.Parameters.AddWithValue("@p", procId ?? 0);
            cmd.Parameters.AddWithValue("@e", equId ?? 0);
            cmd.Parameters.AddWithValue("@c", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
