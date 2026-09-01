using Microsoft.Data.SqlClient;
using System.Data;

namespace RBI_Malaysia.Services;

public class InspectionService
{
    private readonly IConfiguration _configuration;

    public InspectionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("connString")
        ?? throw new InvalidOperationException(
            "Connection string 'connString' was not found.");

    // =========================================================
    // GET INSPECTION GRID
    // =========================================================
    public async Task<List<InspectionModel>> GetInspectionAsync(
        decimal companyId,
        decimal processAreaId = 0,
        string? equpId = null,
        string? componentNo = null)
    {
        var list = new List<InspectionModel>();

        string mode;

        if (!string.IsNullOrWhiteSpace(componentNo))
            mode = "GRID_COMPONENT";
        else if (!string.IsNullOrWhiteSpace(equpId))
            mode = "GRID_EQUIPMENT";
        else if (processAreaId > 0)
            mode = "GRID_PROCESS";
        else
            mode = "GRID";

        await using var con = new SqlConnection(ConnectionString);

        await using var cmd =
            new SqlCommand("sp_Inspection", con);

        cmd.CommandType =
            CommandType.StoredProcedure;

        cmd.CommandTimeout = 30;

        cmd.Parameters.Add("@Mode", SqlDbType.VarChar, 30)
            .Value = mode;

        cmd.Parameters.Add("@InspecAutoID", SqlDbType.Decimal)
            .Value = 0;

        cmd.Parameters.Add("@ProcessAreaID", SqlDbType.Decimal)
            .Value = processAreaId;

        cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
            .Value = string.IsNullOrWhiteSpace(equpId)
                ? DBNull.Value
                : equpId;

        cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
            .Value = string.IsNullOrWhiteSpace(componentNo)
                ? DBNull.Value
                : componentNo;

        cmd.Parameters.Add("@InspectionPointNo", SqlDbType.NVarChar, 50)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@InspecDate", SqlDbType.SmallDateTime)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@ReadingValue", SqlDbType.Decimal)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@CompanyID", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@UserID", SqlDbType.Decimal)
            .Value = 0;

        await con.OpenAsync();

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(MapInspection(reader));
        }

        return list;
    }


    // =========================================================
    // LAST INSPECTION
    // =========================================================
    public async Task<List<InspectionModel>> GetLastInspectionAsync(
     decimal companyId,
     string equpId,
     string componentNo)
    {
        var list = new List<InspectionModel>();
        await using var con = new SqlConnection(ConnectionString);

        await using var cmd = new SqlCommand(
            "sp_Inspection",
            con);

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 30;

        // =====================================================
        // LAST - EXACT PARAMETER MAPPING
        // =====================================================

        cmd.Parameters.Add("@Mode", SqlDbType.VarChar, 30)
            .Value = "LAST";

        cmd.Parameters.Add("@InspecAutoID", SqlDbType.Decimal)
            .Value = 0;

        // LAST query-க்கு ProcessArea தேவையில்லை
        cmd.Parameters.Add("@ProcessAreaID", SqlDbType.Decimal)
            .Value = 0;

        cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
            .Value = equpId;

        cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
            .Value = componentNo;

        cmd.Parameters.Add("@InspectionPointNo", SqlDbType.NVarChar, 50)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@InspecDate", SqlDbType.SmallDateTime)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@ReadingValue", SqlDbType.Decimal)
            .Value = DBNull.Value;

        cmd.Parameters.Add("@CompanyID", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@UserID", SqlDbType.Decimal)
            .Value = 0;

        await con.OpenAsync();

        await using var reader =
            await cmd.ExecuteReaderAsync();


        while (await reader.ReadAsync())
        {
            list.Add(MapInspection(reader));
        }

        return list;

        //if (await reader.ReadAsync())
        //{
        //    return MapInspection(reader);
        //}

        //return list;
    }


    // =========================================================
    // ALL INSPECTION
    // =========================================================
    public async Task<List<InspectionModel>> GetAllInspectionAsync(
        decimal companyId,
        decimal processAreaId,
        string equpId,
        string componentNo)
    {
        return await ExecuteInspectionQueryAsync(
            "ALL",
            companyId,
            processAreaId,
            equpId,
            componentNo);
    }


    // =========================================================
    // COMMON QUERY
    // =========================================================
    private async Task<List<InspectionModel>> ExecuteInspectionQueryAsync(
        string mode,
        decimal companyId,
        decimal processAreaId,
        string? equpId,
        string? componentNo)
    {
        var list = new List<InspectionModel>();

        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand("sp_Inspection", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameters(
            cmd,
            mode,
            0,
            processAreaId,
            equpId,
            componentNo,
            null,
            null,
            null,
            companyId,
            0);

        await con.OpenAsync();

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(MapInspection(reader));
        }

        return list;
    }


    // =========================================================
    // INSERT
    // =========================================================
    public async Task<InspectionResult> InsertInspectionAsync(InspectionModel model, decimal companyId, decimal userId)
    {
        return await SaveInspectionAsync("INSERT", model, companyId, userId);
    }


    // =========================================================
    // UPDATE
    // =========================================================
    public async Task<InspectionResult> UpdateInspectionAsync(
        InspectionModel model,
        decimal companyId,
        decimal userId)
    {
        return await SaveInspectionAsync(
            "UPDATE",
            model,
            companyId,
            userId);
    }


    // =========================================================
    // DELETE
    // =========================================================
    public async Task<InspectionResult> DeleteInspectionAsync(
        decimal inspectionId,
        decimal companyId,
        decimal userId)
    {
        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand("sp_Inspection", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameters(
            cmd,
            "DELETE",
            inspectionId,
            0,
            null,
            null,
            null,
            null,
            null,
            companyId,
            userId);

        await con.OpenAsync();

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new InspectionResult
            {
                Result = GetInt(reader, "Result"),
                Message = GetString(reader, "Message")
            };
        }

        return new InspectionResult
        {
            Result = 1,
            Message = "Inspection deleted successfully."
        };
    }


    // =========================================================
    // INSERT / UPDATE
    // =========================================================
    private async Task<InspectionResult> SaveInspectionAsync(string mode, InspectionModel model, decimal companyId, decimal userId)
    {
        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand("sp_Inspection", con);

        cmd.CommandType = CommandType.StoredProcedure;

        AddParameters(
            cmd,
            mode,
            model.InspecAutoID,
            model.ProcessAreaID,
            model.EqupID,
            model.ComponentNo,
            model.InspectionPointNo,
            model.InspecDate,
            model.ReadingValue,
            companyId,
            userId);

        await con.OpenAsync();

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new InspectionResult
            {
                Result = GetInt(reader, "Result"),
                Message = GetString(reader, "Message")
            };
        }

        return new InspectionResult
        {
            Result = 1,
            Message = mode == "INSERT"
                ? "Inspection inserted successfully."
                : "Inspection updated successfully."
        };
    }



    // =========================================================
    // InspectionCalculation
    // ========================================================= 

    public async Task<InspectionModel?> GetInspectionCalculationAsync(
    decimal processAreaId,
    string equpId,
    string componentNo)
    {
        await using var con = new SqlConnection(ConnectionString);

        await con.OpenAsync();

        // =========================================================
        // 1. COMPONENT DETAILS
        // =========================================================

        decimal shortCR = 0;
        decimal longCR = 0;
        decimal uCR = 0;
        decimal defaultValue = 0;
        decimal remainingLife = 0;

        await using (var cmd = new SqlCommand(@"
        SELECT
            ShortCRrate,
            LongCRrate,
            uCR,
            Defaultvalue,
            Remaininglife
        FROM Tbl_EquipmentComponentDetails
        WHERE ProcessAreaID = @ProcessAreaID
          AND EqupID = @EqupID
          AND CompAutoID = @ComponentNo
          AND Deleted = 0", con))
        {
            cmd.Parameters.Add("@ProcessAreaID", SqlDbType.Decimal)
                .Value = processAreaId;

            cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
                .Value = equpId;

            cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
                .Value = componentNo;

            await using var reader =
                await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                shortCR =
                    reader["ShortCRrate"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["ShortCRrate"]);

                longCR =
                    reader["LongCRrate"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["LongCRrate"]);

                uCR =
                    reader["uCR"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["uCR"]);

                defaultValue =
                    reader["Defaultvalue"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["Defaultvalue"]);

                remainingLife =
                    reader["Remaininglife"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["Remaininglife"]);
            }
        }


        // =========================================================
        // 2. CURRENT THICKNESS
        //
        // First:
        //     min ReadingValue using DefaultsCR + DefaultLCR
        //
        // If not found:
        //     min ReadingValue using uCR
        // =========================================================

        decimal? currentThickness = null;


        // ---------------------------------------------------------
        // FIRST QUERY
        // ---------------------------------------------------------

        await using (var cmd = new SqlCommand(@"
        SELECT MIN(ReadingValue)
        FROM Tbl_InspectionDataDetails
        WHERE EqupID = @EqupID
          AND ComponentNo = @ComponentNo
          AND DefaultsCR = @DefaultsCR
          AND DefaultlCR = @DefaultLCR
          AND Deleted = 0", con))
        {
            cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
                .Value = equpId;

            cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
                .Value = componentNo;

            cmd.Parameters.Add("@DefaultsCR", SqlDbType.Decimal)
                .Value = shortCR;

            cmd.Parameters.Add("@DefaultLCR", SqlDbType.Decimal)
                .Value = longCR;

            var result = await cmd.ExecuteScalarAsync();

            if (result != null &&
                result != DBNull.Value)
            {
                currentThickness =
                    Convert.ToDecimal(result);
            }
        }


        // ---------------------------------------------------------
        // SECOND QUERY
        //
        // First query result இல்லையென்றால் மட்டும்
        // ---------------------------------------------------------

        if (!currentThickness.HasValue)
        {
            await using var cmd = new SqlCommand(@"
            SELECT MIN(ReadingValue)
            FROM Tbl_InspectionDataDetails
            WHERE EqupID = @EqupID
              AND ComponentNo = @ComponentNo
              AND uCR = @UCR
              AND Deleted = 0", con);

            cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
                .Value = equpId;

            cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
                .Value = componentNo;

            cmd.Parameters.Add("@UCR", SqlDbType.Decimal)
                .Value = uCR;

            var result =
                await cmd.ExecuteScalarAsync();

            if (result != null &&
                result != DBNull.Value)
            {
                currentThickness =
                    Convert.ToDecimal(result);
            }
        }


        // =========================================================
        // RETURN CALCULATION MODEL
        // =========================================================

        return new InspectionModel
        {
            ShortCRrate = shortCR,
            LongCRrate = longCR,
            UCR = uCR,
            DefaultsCR = shortCR,
            DefaultLCR = longCR,
            RemainingLife = remainingLife,
            CurrentThickness = currentThickness,
            Defaultvalue = defaultValue
        };
    }


    // =========================================================
    // PARAMETERS
    // =========================================================
    private static void AddParameters(
        SqlCommand cmd,
        string mode,
        decimal inspectionId,
        decimal processAreaId,
        string? equpId,
        string? componentNo,
        string? inspectionPointNo,
        DateTime? inspectionDate,
        decimal? readingValue,
        decimal companyId,
        decimal userId)
    {
        cmd.Parameters.Add("@Mode", SqlDbType.VarChar, 30)
            .Value = mode;

        cmd.Parameters.Add("@InspecAutoID", SqlDbType.Decimal)
            .Value = inspectionId;

        cmd.Parameters.Add("@ProcessAreaID", SqlDbType.Decimal)
            .Value = processAreaId;

        cmd.Parameters.Add("@EqupID", SqlDbType.NVarChar, 20)
            .Value = string.IsNullOrWhiteSpace(equpId)
                ? DBNull.Value
                : equpId;

        cmd.Parameters.Add("@ComponentNo", SqlDbType.NVarChar, 20)
            .Value = string.IsNullOrWhiteSpace(componentNo)
                ? DBNull.Value
                : componentNo;

        cmd.Parameters.Add("@InspectionPointNo", SqlDbType.NVarChar, 50)
            .Value = string.IsNullOrWhiteSpace(inspectionPointNo)
                ? DBNull.Value
                : inspectionPointNo;

        cmd.Parameters.Add("@InspecDate", SqlDbType.SmallDateTime)
            .Value = inspectionDate.HasValue
                ? inspectionDate.Value
                : DBNull.Value;

        cmd.Parameters.Add("@ReadingValue", SqlDbType.Decimal)
            .Value = readingValue.HasValue
                ? readingValue.Value
                : DBNull.Value;

        cmd.Parameters.Add("@CompanyID", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@UserID", SqlDbType.Decimal)
            .Value = userId;
    }


    // =========================================================
    // MAP INSPECTION
    // =========================================================
    private static InspectionModel MapInspection(SqlDataReader reader)
    {
        return new InspectionModel
        {
            InspecAutoID = GetDecimal(reader, "InspecAutoID"),

            EqupID = GetString(reader, "EquAutoID"),
            ComponentNo = GetString(reader, "CompAutoID"),

            ProcessAreaID = GetDecimal(reader, "ProcessareaID"),

            EquipmentName = GetString(reader, "EqupType"),
            ComponentName = GetString(reader, "CompName"),
            ProcessAreaName = GetString(reader, "ProcessArea"),

            InspectionPointNo =
                GetString(reader, "InspectionPointNo"),

            InspecDate =
                GetDateTime(reader, "InspecDate"),

            ReadingValue =
                GetNullableDecimal(reader, "ReadingValue"),

            PreviousDate =
                GetDateTime(reader, "Previousdate"),

            PreviousValue =
                GetNullableDecimal(reader, "Previousvalue"),

            InitialDate =
                GetDateTime(reader, "Initialdate"),

            InitialValue =
                GetNullableDecimal(reader, "Initialvalue"),

            ShortCRrate =
                GetNullableDecimal(reader, "ShortCRrate"),

            LongCRrate =
                GetNullableDecimal(reader, "LongCRrate"),

            DefaultsCR =
                GetNullableDecimal(reader, "DefaultsCR"),

            DefaultLCR =
                GetNullableDecimal(reader, "DefaultlCR"),

            SCR =
                GetNullableDecimal(reader, "SCR"),

            LCR =
                GetNullableDecimal(reader, "LCR"),

            DSCR =
                GetNullableDecimal(reader, "DSCR"),

            DLCR =
                GetNullableDecimal(reader, "DLCR"),

            UCR =
                GetNullableDecimal(reader, "uCR"),

            RemainingLife =
                GetNullableDecimal(reader, "RemainingLife"),

            CalculatedUCR =
                GetNullableDecimal(reader, "CalculatedUCR")
        };
    }


    // =========================================================
    // SAFE READER METHODS
    // =========================================================
    private static bool HasColumn(
        SqlDataReader reader,
        string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(
                reader.GetName(i),
                columnName,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static string? GetString(
        SqlDataReader reader,
        string columnName)
    {
        if (!HasColumn(reader, columnName))
            return null;

        int index = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(index))
            return null;

        return Convert.ToString(reader.GetValue(index));
    }


    private static decimal GetDecimal(
        SqlDataReader reader,
        string columnName)
    {
        if (!HasColumn(reader, columnName))
            return 0;

        int index = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(index))
            return 0;

        return Convert.ToDecimal(reader.GetValue(index));
    }


    private static decimal? GetNullableDecimal(
        SqlDataReader reader,
        string columnName)
    {
        if (!HasColumn(reader, columnName))
            return null;

        int index = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(index))
            return null;

        return Convert.ToDecimal(reader.GetValue(index));
    }


    private static DateTime? GetDateTime(
        SqlDataReader reader,
        string columnName)
    {
        if (!HasColumn(reader, columnName))
            return null;

        int index = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(index))
            return null;

        return Convert.ToDateTime(reader.GetValue(index));
    }


    private static int GetInt(
        SqlDataReader reader,
        string columnName)
    {
        if (!HasColumn(reader, columnName))
            return 0;

        int index = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(index))
            return 0;

        return Convert.ToInt32(reader.GetValue(index));
    }
}


// =============================================================
// INSPECTION MODEL
// =============================================================
public class InspectionModel
{
    public decimal InspecAutoID { get; set; }

    public decimal ProcessAreaID { get; set; }

    public string? ProcessAreaName { get; set; }

    public string? EqupID { get; set; }

    public string? EquipmentName { get; set; }

    public string? ComponentNo { get; set; }

    public string? ComponentName { get; set; }

    public string? InspectionPointNo { get; set; }

    public DateTime? InspecDate { get; set; }

    public decimal? ReadingValue { get; set; }

    public DateTime? PreviousDate { get; set; }

    public decimal? PreviousValue { get; set; }

    public DateTime? InitialDate { get; set; }

    public decimal? InitialValue { get; set; }

    public decimal? ShortCRrate { get; set; }

    public decimal? LongCRrate { get; set; }

    public decimal? DefaultsCR { get; set; }

    public decimal? DefaultLCR { get; set; }

    public decimal? SCR { get; set; }

    public decimal? LCR { get; set; }

    public decimal? DSCR { get; set; }

    public decimal? DLCR { get; set; }

    public decimal? UCR { get; set; }

    public decimal? CalculatedUCR { get; set; }

    public decimal? RemainingLife { get; set; }

    public decimal? CompanyID { get; set; }

    public decimal? CreatedBy { get; set; }

    public decimal? CurrentThickness { get; set; }

    public decimal? Defaultvalue { get; set; }
}


// =============================================================
// RESULT
// =============================================================
public class InspectionResult
{
    public int Result { get; set; }

    public string? Message { get; set; }

    public bool Success => Result == 1;
}


// =============================================================
// FILTER MODELS
// =============================================================
public class InspectionProcessArea
{
    public decimal ID { get; set; }

    public string? ProcessArea { get; set; }
}


public class InspectionEquipment
{
    public string? EqupID { get; set; }

    public string? EquipmentName { get; set; }
}


public class InspectionComponent
{
    public string? ComponentNo { get; set; }

    public string? ComponentName { get; set; }
}