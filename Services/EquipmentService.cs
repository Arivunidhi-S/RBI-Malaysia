using Microsoft.Data.SqlClient;
using System.Data;

namespace RBI_Malaysia.Services;

public class EquipmentService
{
    private readonly IConfiguration _configuration;

    public EquipmentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("connString")
        ?? throw new InvalidOperationException(
            "Connection string 'connString' was not found.");

    // =========================================================
    // GET EQUIPMENT LIST
    // =========================================================
    public async Task<List<EquipmentModel>> GetEquipmentsAsync(
        decimal companyId)
    {
        var list = new List<EquipmentModel>();

        using SqlConnection conn = new(ConnectionString);
        using SqlCommand cmd = new("sp_Get_tbl_Val", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@id", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@tblflg", SqlDbType.NVarChar, 10)
            .Value = "EquGrid";

        await conn.OpenAsync();

        using SqlDataReader reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new EquipmentModel
            {
                EquAutoID = GetDecimal(reader, "EquAutoID"),

                ProcessAreaID = GetDecimal(reader, "ProcessAreaID"),

                ProcessArea = GetString(reader, "ProcessArea"),

                EquPID = GetString(reader, "EquPID"),

                EqupType = GetString(reader, "EqupType"),

                EqupDescription =
                    GetString(reader, "EqupDescription"),

                DoshNo = GetString(reader, "DoshNo"),

                PID = GetString(reader, "PID"),

                IPLayer = GetString(reader, "IPLayer"),

                Instructive =
                    GetString(reader, "Instructive"),

                YearInstalled =
                    GetString(reader, "YearInstalled"),

                WTM = GetBool(reader, "WTM"),

                HistoryDescription =
                    GetString(reader, "historydescription"),

                InspectionTechniques =
                    GetString(reader, "InspectionTechniques"),

                InspectionScope =
                    GetString(reader, "Inspectionscope"),

                RBIObservation =
                    GetString(reader, "RBIobservation"),

                DOSHObservation =
                    GetString(reader, "DOSHobservation"),

                DesignCode =
                    GetString(reader, "DesignCode"),

                CompanyID =
                    GetDecimal(reader, "CompanyID")
            });
        }

        return list;
    }

    // =========================================================
    // GET PROCESS AREAS
    // =========================================================
    public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(decimal companyId)
    {
        var list = new List<ProcessAreaModel>();

        using SqlConnection conn = new(ConnectionString);

        using SqlCommand cmd = new("sp_Get_tbl_Val", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@id", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@tblflg", SqlDbType.NVarChar, 10)
            .Value = "ProAreaSel";

        await conn.OpenAsync();

        using SqlDataReader reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new ProcessAreaModel
            {
                ProcessAreaID =
                    Convert.ToDecimal(reader["ProcessAreaID"]),

                ProcessArea =
                    reader["ProcessArea"]?.ToString() ?? ""
            });
        }

        return list;
    }

    // =========================================================
    // SAVE / UPDATE / DELETE
    // =========================================================
    public async Task SaveEquipmentAsync(
        EquipmentModel equipment,
        decimal userId,
        string saveFlag)
    {
        using SqlConnection conn = new(ConnectionString);

        using SqlCommand cmd =
            new("sp_EquipmentMaster_Save", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@idp", SqlDbType.Decimal)
            .Value = equipment.EquAutoID;

        cmd.Parameters.Add("@processareap", SqlDbType.Decimal)
            .Value = equipment.ProcessAreaID;

        cmd.Parameters.Add("@EquipIDp", SqlDbType.NVarChar, 50)
            .Value = equipment.EquPID ?? "";

        cmd.Parameters.Add("@Equiptypep", SqlDbType.NVarChar, 50)
            .Value = equipment.EqupType ?? "";

        cmd.Parameters.Add("@descriptionp", SqlDbType.NVarChar)
            .Value = equipment.EqupDescription ?? "";

        cmd.Parameters.Add("@doshnop", SqlDbType.NVarChar, 50)
            .Value = equipment.DoshNo ?? "";

        cmd.Parameters.Add("@PIDp", SqlDbType.NVarChar, 50)
            .Value = equipment.PID ?? "";

        cmd.Parameters.Add("@IPLayerp", SqlDbType.NVarChar, 50)
            .Value = equipment.IPLayer ?? "None";

        cmd.Parameters.Add("@Intrusivep", SqlDbType.NVarChar, 50)
            .Value = equipment.Instructive ?? "None";

        cmd.Parameters.Add("@yearinstallp", SqlDbType.NVarChar, 20)
            .Value = equipment.YearInstalled ?? "";

        cmd.Parameters.Add("@WTMp", SqlDbType.NVarChar, 20)
            .Value = equipment.WTM ? "1" : "0";

        cmd.Parameters.Add("@histdescripp", SqlDbType.NVarChar)
            .Value = equipment.HistoryDescription ?? "";

        cmd.Parameters.Add("@inspectechp", SqlDbType.NVarChar)
            .Value = equipment.InspectionTechniques ?? "";

        cmd.Parameters.Add("@inspecscopep", SqlDbType.NVarChar)
            .Value = equipment.InspectionScope ?? "";

        cmd.Parameters.Add("@RBIObservp", SqlDbType.NVarChar)
            .Value = equipment.RBIObservation ?? "";

        cmd.Parameters.Add("@DOSHobservp", SqlDbType.NVarChar)
            .Value = equipment.DOSHObservation ?? "";

        cmd.Parameters.Add("@DesignCodep", SqlDbType.NVarChar)
            .Value = equipment.DesignCode ?? "";

        // Legacy code currently sends byte 0.
        cmd.Parameters.Add("@EquipImagep", SqlDbType.Binary, 50)
            .Value = new byte[] { 0 };

        cmd.Parameters.Add("@CompanyID", SqlDbType.Decimal)
            .Value = equipment.CompanyID;

        cmd.Parameters.Add("@useridp", SqlDbType.NVarChar, 50)
            .Value = userId.ToString();

        cmd.Parameters.Add("@saveflag", SqlDbType.NVarChar, 50)
            .Value = saveFlag;

        await conn.OpenAsync();

        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================
    // DELETE
    // =========================================================
    public async Task DeleteEquipmentAsync(
        decimal equipmentId,
        decimal companyId,
        decimal userId)
    {
        var equipment = new EquipmentModel
        {
            EquAutoID = equipmentId,
            CompanyID = companyId
        };

        await SaveEquipmentAsync(
            equipment,
            userId,
            "D");
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private static string GetString(
        SqlDataReader reader,
        string column)
    {
        int index = reader.GetOrdinal(column);

        return reader.IsDBNull(index)
            ? ""
            : reader[index]?.ToString() ?? "";
    }

    private static decimal GetDecimal(
        SqlDataReader reader,
        string column)
    {
        int index = reader.GetOrdinal(column);

        return reader.IsDBNull(index)
            ? 0
            : Convert.ToDecimal(reader[index]);
    }

    private static bool GetBool(
        SqlDataReader reader,
        string column)
    {
        int index = reader.GetOrdinal(column);

        if (reader.IsDBNull(index))
            return false;

        object value = reader[index];

        if (value is bool b)
            return b;

        return value.ToString() == "1" ||
               value.ToString()
                    .Equals("true",
                        StringComparison.OrdinalIgnoreCase);
    }
}

// =============================================================
// PROCESS AREA MODEL
// =============================================================
//public class ProcessAreaModel
//{
//    public decimal ProcessAreaID { get; set; }

//    public string ProcessArea { get; set; } = "";
//}