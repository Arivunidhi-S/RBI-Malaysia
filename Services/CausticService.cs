using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class CausticService
    {
        private readonly string _connectionString;

        public CausticService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // Susceptibility label → numeric Svi value (matches old ASP.Net dropdown exactly)
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

        public async Task<CausticModel?> GetCausticRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT * FROM CausticCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0", conn);
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

            return new CausticModel
            {
                CauID = rd.GetDecimal(rd.GetOrdinal("CauID")),
                ProcID = procId, EquID = equId, CompID = compId,
                CSAge = Int("CSAge"),
                CSInsEff = S("CSInsEff"),
                CSnofIns = Int("CSnofIns"),
                InspectDate = Dt("InspectDate"),
                CSSvi = S("CSSvi"),
                CSSviVal = Int("CSSviVal"),
                CSDf = Dec("CSDf"),
            };
        }

        // =================================================================
        // 3. CALCULATE (no DB write)
        //    Formula (exact match to old code line 1032-1038):
        //    dfcs  = Ref_SCC[ InsEff_column, Svi=CSSviVal, Inspection=CSnofIns ]
        //    CSDf  = dfcs × Age^1.1
        // =================================================================

        public async Task<CausticModel> CalculateCausticAsync(CausticModel m)
        {
            // Validation
            if (string.IsNullOrEmpty(m.CSInsEff)) throw new InvalidOperationException("Inspection Effectiveness is required.");
            if (m.CSSviVal == null) throw new InvalidOperationException("Susceptibility (Svi) is required.");
            if (m.CSnofIns == null) throw new InvalidOperationException("No. of Inspections is required.");
            if (m.CSAge == null || m.CSAge <= 0) throw new InvalidOperationException("Age is required and must be > 0.");

            // Only A/B/C/D/E are valid column names — validated against allowlist to prevent SQL injection
            string col = m.CSInsEff switch { "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E",
                _ => throw new InvalidOperationException($"Invalid Inspection Effectiveness value: {m.CSInsEff}") };

            string sql = $"SELECT {col} AS dfcs FROM Ref_SCC WHERE Svi=@svi AND Inspection=@nofins";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@svi", m.CSSviVal.Value);
            cmd.Parameters.AddWithValue("@nofins", m.CSnofIns.Value);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();

            if (!await rd.ReadAsync() || rd["dfcs"] == DBNull.Value)
                throw new InvalidOperationException("No matching record found in Ref_SCC for the selected Svi and No. of Inspections.");

            double dfcs = Convert.ToDouble(rd["dfcs"]);
            double dfb_cs = dfcs * Math.Pow(m.CSAge.Value, 1.1);
            m.CSDf = Convert.ToDecimal(Math.Round(dfb_cs, 3));

            return m;
        }

        // =================================================================
        // 4. SAVE (separate)
        // =================================================================

        public async Task<CausticModel> SaveCausticAsync(CausticModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            int count = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(1) FROM CausticCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c AND Deleted=0",
                conn) { Parameters = { new("@p", m.ProcID ?? 0), new("@e", m.EquID ?? 0), new("@c", m.CompID ?? 0) } }
                .ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE CausticCracking SET
CSAge=@CSAge, CSInsEff=@CSInsEff, CSnofIns=@CSnofIns, InspectDate=@InspectDate,
CSSvi=@CSSvi, CSSviVal=@CSSviVal, CSDf=@CSDf,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO CausticCracking
(ProcID, EquID, CompID, CSAge, CSInsEff, CSnofIns, InspectDate, CSSvi, CSSviVal, CSDf,
 Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @CSAge, @CSInsEff, @CSnofIns, @InspectDate, @CSSvi, @CSSviVal, @CSDf,
 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID",  m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID",   m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID",  m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@CSAge",   (object?)m.CSAge ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CSInsEff",(object?)m.CSInsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CSnofIns",(object?)m.CSnofIns ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate",(object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CSSvi",   (object?)m.CSSvi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CSSviVal",(object?)m.CSSviVal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CSDf",    (object?)m.CSDf ?? DBNull.Value);

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

        public async Task<bool> DeleteCausticAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM CausticCracking WHERE ProcID=@p AND EquID=@e AND CompID=@c", conn);
            cmd.Parameters.AddWithValue("@p", procId ?? 0);
            cmd.Parameters.AddWithValue("@e", equId ?? 0);
            cmd.Parameters.AddWithValue("@c", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
