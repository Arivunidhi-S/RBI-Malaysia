using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class InspectionPlanService
    {
        private readonly string _connectionString;

        public InspectionPlanService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // Damage Factor options — all damage mechanisms that feed InspectionPlan
        public static readonly List<string> DamageFactorOptions = new()
        {
            "Thinning", "Caustic", "Amine", "Sulfide", "H2S", "Carbonate",
            "PTA", "CLSCC", "HSC-HF", "HIC/SOHIC-HF", "CUI", "ECD"
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
        // 2. LOAD LIST — filtered by Process Area / Equipment / Component
        // =================================================================

        public async Task<List<InspectionPlanModel>> GetInspectionPlansAsync(
            decimal procId, decimal equId, decimal compId)
        {
            var list = new List<InspectionPlanModel>();
            string sql = @"
                SELECT ip.InspectID, ip.ProcID, ip.EquID, ip.CompID,
                       ip.DamageFact, ip.InspectCate, ip.InspectDate,
                       ie.InsEffCate, ie.IntrInsp, ie.NonIntrInsp,
                       pa.processarea,
                       ea.EqupID + ' - ' + ea.EqupType AS EquipName,
                       cd.CompNo  + ' - ' + cd.compname  AS CompName
                FROM InspectionPlan ip
                LEFT JOIN InspectEffect ie
                    ON ie.DamageFactor = ip.DamageFact AND ie.InsCate = ip.InspectCate
                LEFT JOIN Tbl_ProcessArea               pa ON pa.ProcessAreaID = ip.ProcID
                LEFT JOIN Tbl_EquipmentAsset            ea ON ea.EquAutoID     = ip.EquID
                LEFT JOIN Tbl_EquipmentComponentDetails cd ON cd.compautoid    = ip.CompID
                WHERE (@ProcID = 0 OR ip.ProcID = @ProcID)
                  AND (@EquID  = 0 OR ip.EquID  = @EquID)
                  AND (@CompID = 0 OR ip.CompID = @CompID)
                ORDER BY pa.processarea, ea.EqupID, cd.CompNo, ip.DamageFact";

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                string? S(string c) => rd[c] == DBNull.Value ? null : rd[c].ToString()?.Trim();
                DateTime? Dt(string c) => rd[c] == DBNull.Value ? null : Convert.ToDateTime(rd[c]);

                list.Add(new InspectionPlanModel
                {
                    InspectID = Convert.ToDecimal(rd["InspectID"]),
                    ProcID = Convert.ToDecimal(rd["ProcID"]),
                    EquID = Convert.ToDecimal(rd["EquID"]),
                    CompID = Convert.ToDecimal(rd["CompID"]),
                    DamageFact = S("DamageFact"),
                    InspectCate = S("InspectCate"),
                    InspectDate = Dt("InspectDate"),
                    InsEffCate = S("InsEffCate"),
                    IntrInsp = S("IntrInsp"),
                    NonIntrInsp = S("NonIntrInsp"),
                    ProcessArea = S("processarea"),
                    EquipmentName = S("EquipName"),
                    ComponentName = S("CompName"),
                });
            }
            return list;
        }

        // =================================================================
        // 3. LOOKUP — InspectEffect recommendation when DamageFact + InspectCate change
        // =================================================================

        public async Task<(string insEffCate, string intrInsp, string nonIntrInsp)> GetInspectEffectAsync(
            string damageFactor, string inspectCate)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "SELECT InsEffCate, IntrInsp, NonIntrInsp FROM InspectEffect WHERE DamageFactor=@df AND InsCate=@ic", conn);
            cmd.Parameters.AddWithValue("@df", damageFactor);
            cmd.Parameters.AddWithValue("@ic", inspectCate);
            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                string S(string c) => rd[c] == DBNull.Value ? "" : rd[c].ToString()?.Trim() ?? "";
                return (S("InsEffCate"), S("IntrInsp"), S("NonIntrInsp"));
            }
            return ("", "", "");
        }

        // =================================================================
        // 4. SAVE (INSERT or UPDATE)
        // =================================================================

        public async Task<InspectionPlanModel> SaveInspectionPlanAsync(InspectionPlanModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check existing by ProcID + EquID + CompID + DamageFact (compound key)
            int count = Convert.ToInt32(await new SqlCommand(
                "SELECT COUNT(1) FROM InspectionPlan WHERE ProcID=@p AND EquID=@e AND CompID=@c AND DamageFact=@df",
                conn)
            {
                Parameters =
                {
                    new("@p",  m.ProcID ?? 0),
                    new("@e",  m.EquID ?? 0),
                    new("@c",  m.CompID ?? 0),
                    new("@df", m.DamageFact ?? "")
                }
            }.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE InspectionPlan SET
InspectCate=@InspectCate, InspectDate=@InspectDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND DamageFact=@DamageFact";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO InspectionPlan (ProcID, EquID, CompID, DamageFact, InspectCate, InspectDate)
VALUES (@ProcID, @EquID, @CompID, @DamageFact, @InspectCate, @InspectDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID",      m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID",       m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID",      m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@DamageFact",  (object?)m.DamageFact ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectCate", (object?)m.InspectCate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // Refresh the InspectEffect lookup fields after save
            if (!string.IsNullOrEmpty(m.DamageFact) && !string.IsNullOrEmpty(m.InspectCate))
            {
                var (eff, intr, nonIntr) = await GetInspectEffectAsync(m.DamageFact, m.InspectCate);
                m.InsEffCate = eff;
                m.IntrInsp   = intr;
                m.NonIntrInsp = nonIntr;
            }
            return m;
        }

        // =================================================================
        // 5. DELETE
        // =================================================================

        public async Task<bool> DeleteInspectionPlanAsync(decimal inspectId)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(
                "DELETE FROM InspectionPlan WHERE InspectID=@id", conn);
            cmd.Parameters.AddWithValue("@id", inspectId);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
