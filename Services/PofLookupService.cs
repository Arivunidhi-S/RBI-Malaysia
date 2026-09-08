using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services
{
    /// <summary>
    /// Shared cascading lookups for the Process Area -> Equipment -> Component pickers
    /// used at the top of every POF tab. Ports:
    ///   cboProcessArea_OnItemsRequested       -> GetProcessAreasAsync
    ///   OnSelectedIndexChanged_cboProcess     -> GetEquipmentAsync
    ///   OnSelectedIndexChanged_cboEquipment   -> GetComponentsAsync
    /// Reuse this one service across Thinning / Lining / External / SCC / HTHA / etc.
    /// so the selector behaves identically everywhere.
    /// </summary>
    public class PofLookupService
    {
        private readonly string _connectionString;

        public PofLookupService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not configured.");
        }

        public async Task<List<ProcessAreaOption>> GetProcessAreasAsync(int companyId)
        {
            const string sql = @"SELECT [ProcessAreaID],[processarea]
                                  FROM [Tbl_ProcessArea]
                                  WHERE [deleted] = 0 AND [companyid] = @CompanyID
                                  ORDER BY [processareaid]";

            var list = new List<ProcessAreaOption>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@CompanyID", companyId);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new ProcessAreaOption
                {
                    ProcessAreaID = Convert.ToInt32(rd["ProcessAreaID"]),
                    ProcessArea = rd["processarea"]?.ToString() ?? string.Empty
                });
            }
            return list;
        }

        public async Task<List<EquipmentOption>> GetEquipmentAsync(int processAreaId)
        {
            const string sql = @"SELECT [EquAutoID],[EqupType],[EqupID]
                                  FROM [Tbl_EquipmentAsset]
                                  WHERE [ProcessAreaID] = @ProcessAreaID AND [deleted] = 0
                                  ORDER BY [EqupID]";

            var list = new List<EquipmentOption>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProcessAreaID", processAreaId);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new EquipmentOption
                {
                    EquAutoID = Convert.ToInt32(rd["EquAutoID"]),
                    EqupType = rd["EqupType"]?.ToString() ?? string.Empty,
                    EqupID = rd["EqupID"]?.ToString() ?? string.Empty
                });
            }
            return list;
        }

        public async Task<List<ComponentOption>> GetComponentsAsync(int equipmentId)
        {
            const string sql = @"SELECT [compautoid],[CompNo],[compname],[InspectionEffective],
                                         [NoofInspection],[OPTemp],[Clad],[MRT],[CorrosionAllownce]
                                  FROM [Tbl_EquipmentComponentDetails]
                                  WHERE [EqupID] = @EqupID AND [deleted] = 0";

            var list = new List<ComponentOption>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@EqupID", equipmentId);

            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new ComponentOption
                {
                    CompAutoID = Convert.ToInt32(rd["compautoid"]),
                    CompNo = rd["CompNo"]?.ToString() ?? string.Empty,
                    CompName = rd["compname"]?.ToString() ?? string.Empty,
                    OPTemp = rd["OPTemp"]?.ToString() ?? string.Empty,
                    Clad = rd["Clad"]?.ToString() ?? string.Empty,
                    InspectionEffective = rd["InspectionEffective"]?.ToString() ?? string.Empty,
                    NoofInspection = rd["NoofInspection"]?.ToString() ?? string.Empty,
                    MRT = rd["MRT"]?.ToString() ?? string.Empty,
                    CorrosionAllownce = rd["CorrosionAllownce"]?.ToString() ?? string.Empty
                });
            }
            return list;
        }
    }
}
