using System.Data;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services
{
    public class DashboardService
    {
        private readonly IConfiguration _configuration;

        public DashboardService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            string connectionString = _configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
            return new SqlConnection(connectionString);
        }

        // =========================================================
        // DASHBOARD SUMMARY
        // =========================================================

        public async Task<DashboardModel> GetDashboardSummaryAsync(decimal companyId)
        {
            var model = new DashboardModel();

            await using SqlConnection conn = GetConnection();

            await using SqlCommand cmd = new SqlCommand("sp_Dashboard_GetEquipmentCount", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@CompanyID", SqlDbType.Decimal).Value = companyId;

            await conn.OpenAsync();

            object? result = await cmd.ExecuteScalarAsync();

            if (result != null &&
                result != DBNull.Value)
            {
                model.TotalEquipment = Convert.ToInt32(result);
            }

            return model;
        }

        // =========================================================
        // DASHBOARD SUMMARY
        // =========================================================

        public async Task<DashboardModel> GetDashboardSummaryAsync2(
            decimal companyId)
        {
            DashboardModel dashboard = new();

            await using SqlConnection conn = GetConnection();

            await using SqlCommand cmd =
                new SqlCommand(
                    "sp_Dashboard_GetSummary",
                    conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(
                "@CompanyID",
                SqlDbType.Decimal).Value = companyId;

            await conn.OpenAsync();

            await using SqlDataReader reader =
                await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                dashboard.HighRisk =
                    reader["HighRisk"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["HighRisk"]);

                dashboard.MediumRisk =
                    reader["MediumRisk"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["MediumRisk"]);

                dashboard.LowRisk =
                    reader["LowRisk"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["LowRisk"]);

                dashboard.InspectionStatus =
                    reader["InspectionStatus"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            reader["InspectionStatus"]);

                dashboard.CriticalEquipment =
                    reader["CriticalEquipment"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(
                            reader["CriticalEquipment"]);
            }

            return dashboard;
        }
    }
}