using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System.Data;

namespace RBI_Malaysia.Services
{
    public class LiningDamageService
    {
        private readonly string _connectionString;

        public LiningDamageService(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException(
                    "Connection string 'connString' not found.");
        }

        // =========================================================
        // PROCESS AREA
        // =========================================================

        public async Task<List<ProcessAreaModel>>
            GetProcessAreasAsync(decimal companyId)
        {
            var list = new List<ProcessAreaModel>();

            const string sql = @"
                SELECT
                    ProcessAreaID,
                    ProcessArea
                FROM Tbl_ProcessArea
                WHERE Deleted = 0
                  AND CompanyID = @CompanyID
                ORDER BY ProcessArea";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.CommandTimeout = 15;

            cmd.Parameters.Add(
                "@CompanyID",
                SqlDbType.Decimal).Value = companyId;

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ProcessAreaModel
                {
                    ProcessAreaID =
                        reader["ProcessAreaID"] == DBNull.Value
                            ? 0
                            : Convert.ToDecimal(
                                reader["ProcessAreaID"]),

                    ProcessArea =
                        reader["ProcessArea"] == DBNull.Value
                            ? string.Empty
                            : reader["ProcessArea"].ToString()!
                });
            }

            return list;
        }

        // =========================================================
        // EQUIPMENT
        // =========================================================

        public async Task<List<EquipmentModel>>
            GetEquipmentsAsync(decimal processAreaId)
        {
            var list = new List<EquipmentModel>();

            const string sql = @"
                SELECT
                    EquAutoID,
                    EqupID,
                    EqupType
                FROM Tbl_EquipmentAsset
                WHERE ProcessAreaID = @ProcessAreaID
                  AND Deleted = 0
                ORDER BY EqupType";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.CommandTimeout = 15;

            cmd.Parameters.Add(
                "@ProcessAreaID",
                SqlDbType.Decimal).Value = processAreaId;

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new EquipmentModel
                {
                    EquAutoID =
                        reader["EquAutoID"] == DBNull.Value
                            ? 0
                            : Convert.ToDecimal(
                                reader["EquAutoID"]),

                    EquPID =
                        $"{reader["EqupID"]} - {reader["EqupType"]}"
                });
            }

            return list;
        }

        // =========================================================
        // COMPONENT
        // =========================================================

        public async Task<List<ComponentModel>>
            GetComponentsAsync(string equipmentId)
        {
            var list = new List<ComponentModel>();

            const string sql = @"
                SELECT
                    CompAutoID,
                    CompNo,
                    CompName
                FROM Tbl_EquipmentComponentDetails
                WHERE EqupID = @EqupID
                  AND Deleted = 0
                ORDER BY CompName";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.CommandTimeout = 15;

            cmd.Parameters.Add(
                "@EqupID",
                SqlDbType.NVarChar,
                20).Value = equipmentId ?? "";

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new ComponentModel
                {
                    CompAutoID =
                        reader["CompAutoID"] == DBNull.Value
                            ? 0
                            : Convert.ToDecimal(
                                reader["CompAutoID"]),

                    CompNo =
                        $"{reader["CompNo"]} - {reader["CompName"]}"
                });
            }

            return list;
        }

        // =========================================================
        // GET EXISTING LINING
        // =========================================================

        public async Task<LiningDamageModel?>
            GetLiningAsync(
                decimal procId,
                decimal equId,
                decimal compId)
        {
            const string sql = @"
                SELECT TOP 1
                    LnfID,
                    ProcID,
                    EquID,
                    CompID,
                    Lntype,
                    SIyear,
                    Dfb,
                    LnCond,
                    LnCondVal,
                    OnMoni,
                    OnMoniVal,
                    DfLine,
                    CompanyID,
                    CreatedBy,
                    CreatedDate,
                    ModifiedBy,
                    ModifiedDate,
                    Rowversions,
                    Deleted
                FROM LiningDamage
                WHERE ProcID = @ProcID
                  AND EquID = @EquID
                  AND CompID = @CompID
                  AND Deleted = 0
                ORDER BY LnfID DESC";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@ProcID",
                SqlDbType.Decimal).Value = procId;

            cmd.Parameters.Add(
                "@EquID",
                SqlDbType.Decimal).Value = equId;

            cmd.Parameters.Add(
                "@CompID",
                SqlDbType.Decimal).Value = compId;

            await con.OpenAsync();

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new LiningDamageModel
            {
                LnfID = ToDecimal(reader["LnfID"]),

                ProcID = ToDecimal(reader["ProcID"]),
                EquID = ToDecimal(reader["EquID"]),
                CompID = ToDecimal(reader["CompID"]),

                Lntype = ToString(reader["Lntype"]),

                SIyear = Convert.ToInt32(
                    ToDecimal(reader["SIyear"])),

                Dfb = ToString(reader["Dfb"]),

                LnCond = ToString(reader["LnCond"]),
                LnCondVal = ToDecimal(reader["LnCondVal"]),

                OnMoni = ToString(reader["OnMoni"]),
                OnMoniVal = ToDecimal(reader["OnMoniVal"]),

                DfLine = ToDecimal(reader["DfLine"]),

                CompanyID = ToDecimal(reader["CompanyID"]),

                CreatedBy = ToDecimal(reader["CreatedBy"]),
                CreatedDate =
                    reader["CreatedDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["CreatedDate"]),

                ModifiedBy = ToDecimal(reader["ModifiedBy"]),
                ModifiedDate =
                    reader["ModifiedDate"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(
                            reader["ModifiedDate"]),

                Rowversions =
                    Convert.ToInt32(
                        ToDecimal(reader["Rowversions"])),

                Deleted =
                    reader["Deleted"] != DBNull.Value &&
                    Convert.ToBoolean(reader["Deleted"])
            };
        }

        // =========================================================
        // GET DFB BASE VALUE
        // =========================================================

        public async Task<decimal>
            GetDfbValueAsync(
                string liningType,
                int siYear,
                string dfbValue)
        {
            string tableName;
            string columnName;

            // IMPORTANT:
            // Table/column names cannot be SQL parameters.
            // Therefore whitelist them before putting into SQL.

            if (liningType == "Organic")
            {
                tableName = "Organic";

                columnName = dfbValue switch
                {
                    "agosix" => "agosix",
                    "sixyear" => "sixyear",
                    "threeyear" => "threeyear",
                    _ => throw new ArgumentException(
                        "Invalid Organic Dfb value.")
                };
            }
            else if (liningType == "Inorganic")
            {
                tableName = "Inorganic";

                columnName = dfbValue switch
                {
                    "Alloy" => "Alloy",
                    "Castable" => "Castable",
                    "Severe" => "Severe",
                    "Glass" => "Glass",
                    "Acid" => "Acid",
                    "Fiber" => "Fiber",
                    _ => throw new ArgumentException(
                        "Invalid Inorganic Dfb value.")
                };
            }
            else
            {
                throw new ArgumentException(
                    "Invalid lining type.");
            }

            string sql = $@"
                SELECT [{columnName}]
                FROM [{tableName}]
                WHERE SIyear = @SIyear";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@SIyear",
                SqlDbType.Decimal).Value = siYear;

            await con.OpenAsync();

            var result =
                await cmd.ExecuteScalarAsync();

            if (result == null ||
                result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    $"No Dfb value found for SIyear {siYear}.");
            }

            return Convert.ToDecimal(result);
        }

        // =========================================================
        // SAVE / UPDATE
        // =========================================================

        public async Task SaveAsync(
            LiningDamageModel model,
            decimal companyId,
            decimal userId)
        {
            await using var con =
                new SqlConnection(_connectionString);

            await con.OpenAsync();

            if (model.LnfID > 0)
            {
                const string updateSql = @"
                    UPDATE LiningDamage
                    SET
                        ProcID = @ProcID,
                        EquID = @EquID,
                        CompID = @CompID,
                        Lntype = @Lntype,
                        SIyear = @SIyear,
                        Dfb = @Dfb,
                        LnCond = @LnCond,
                        LnCondVal = @LnCondVal,
                        OnMoni = @OnMoni,
                        OnMoniVal = @OnMoniVal,
                        DfLine = @DfLine,
                        CompanyID = @CompanyID,
                        ModifiedBy = @ModifiedBy,
                        ModifiedDate = GETDATE(),
                        Rowversions = ISNULL(Rowversions, 0) + 1
                    WHERE LnfID = @LnfID
                      AND Deleted = 0";

                await using var cmd =
                    new SqlCommand(updateSql, con);

                AddParameters(cmd, model, companyId);

                cmd.Parameters.Add(
                    "@LnfID",
                    SqlDbType.Decimal).Value =
                    model.LnfID;

                cmd.Parameters.Add(
                    "@ModifiedBy",
                    SqlDbType.Decimal).Value =
                    userId;

                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO LiningDamage
                    (
                        ProcID,
                        EquID,
                        CompID,
                        Lntype,
                        SIyear,
                        Dfb,
                        LnCond,
                        LnCondVal,
                        OnMoni,
                        OnMoniVal,
                        DfLine,
                        CompanyID,
                        CreatedBy,
                        CreatedDate,
                        Rowversions,
                        Deleted
                    )
                    VALUES
                    (
                        @ProcID,
                        @EquID,
                        @CompID,
                        @Lntype,
                        @SIyear,
                        @Dfb,
                        @LnCond,
                        @LnCondVal,
                        @OnMoni,
                        @OnMoniVal,
                        @DfLine,
                        @CompanyID,
                        @CreatedBy,
                        GETDATE(),
                        0,
                        0
                    )";

                await using var cmd =
                    new SqlCommand(insertSql, con);

                AddParameters(cmd, model, companyId);

                cmd.Parameters.Add(
                    "@CreatedBy",
                    SqlDbType.Decimal).Value =
                    userId;

                await cmd.ExecuteNonQueryAsync();
            }
        }

        // =========================================================
        // DELETE
        // =========================================================

        public async Task<bool>
            DeleteAsync(
                decimal lnfId,
                decimal userId)
        {
            const string sql = @"
                UPDATE LiningDamage
                SET
                    Deleted = 1,
                    ModifiedBy = @ModifiedBy,
                    ModifiedDate = GETDATE(),
                    Rowversions = ISNULL(Rowversions, 0) + 1
                WHERE LnfID = @LnfID
                  AND Deleted = 0";

            await using var con =
                new SqlConnection(_connectionString);

            await using var cmd =
                new SqlCommand(sql, con);

            cmd.Parameters.Add(
                "@LnfID",
                SqlDbType.Decimal).Value = lnfId;

            cmd.Parameters.Add(
                "@ModifiedBy",
                SqlDbType.Decimal).Value = userId;

            await con.OpenAsync();

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =========================================================
        // PARAMETERS
        // =========================================================

        private static void AddParameters(
            SqlCommand cmd,
            LiningDamageModel model,
            decimal companyId)
        {
            cmd.Parameters.Add(
                "@ProcID",
                SqlDbType.Decimal).Value =
                model.ProcID;

            cmd.Parameters.Add(
                "@EquID",
                SqlDbType.Decimal).Value =
                model.EquID;

            cmd.Parameters.Add(
                "@CompID",
                SqlDbType.Decimal).Value =
                model.CompID;

            cmd.Parameters.Add(
                "@Lntype",
                SqlDbType.NVarChar, 15).Value =
                model.Lntype;

            cmd.Parameters.Add(
                "@SIyear",
                SqlDbType.Decimal).Value =
                model.SIyear;

            cmd.Parameters.Add(
                "@Dfb",
                SqlDbType.NVarChar, 50).Value =
                model.Dfb;

            cmd.Parameters.Add(
                "@LnCond",
                SqlDbType.NVarChar, 50).Value =
                model.LnCond;

            cmd.Parameters.Add(
                "@LnCondVal",
                SqlDbType.Decimal).Value =
                model.LnCondVal;

            cmd.Parameters.Add(
                "@OnMoni",
                SqlDbType.NVarChar, 5).Value =
                model.OnMoni;

            cmd.Parameters.Add(
                "@OnMoniVal",
                SqlDbType.Decimal).Value =
                model.OnMoniVal;

            cmd.Parameters.Add(
                "@DfLine",
                SqlDbType.Decimal).Value =
                model.DfLine;

            cmd.Parameters.Add(
                "@CompanyID",
                SqlDbType.Decimal).Value =
                companyId;
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static decimal ToDecimal(object value)
        {
            return value == DBNull.Value
                ? 0
                : Convert.ToDecimal(value);
        }

        private static string ToString(object value)
        {
            return value == DBNull.Value
                ? string.Empty
                : value.ToString()?.Trim() ?? string.Empty;
        }
    }
}