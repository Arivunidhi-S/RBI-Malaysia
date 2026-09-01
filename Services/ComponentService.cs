using Microsoft.Data.SqlClient;
using System.Data;

namespace RBI_Malaysia.Services;

public class ComponentService
{
    private readonly IConfiguration _configuration;

    public ComponentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("connString")
        ?? throw new InvalidOperationException(
            "Connection string 'connString' was not found.");

    // =========================================================
    // PROCESS AREA
    // =========================================================

    public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(
        decimal companyId)
    {
        var result = new List<ProcessAreaModel>();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();


        using SqlCommand cmd = new("sp_Get_tbl_Val", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@id", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@tblflg", SqlDbType.NVarChar, 10)
           .Value = "ProAreaSel";

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ProcessAreaModel
            {
                ProcessAreaID =
                    Convert.ToDecimal(reader["ProcessAreaID"]),

                ProcessArea =
                    reader["ProcessArea"]?.ToString() ?? ""
            });
        }

        return result;
    }

    // =========================================================
    // EQUIPMENT BY PROCESS AREA
    // =========================================================

    public async Task<List<EquipmentLookupModel>> GetEquipmentsAsync(
        decimal processAreaId)
    {
        var result = new List<EquipmentLookupModel>();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();       

        using SqlCommand cmd = new("sp_Get_tbl_Val", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@id", SqlDbType.Decimal)
            .Value = processAreaId;

        cmd.Parameters.Add("@tblflg", SqlDbType.NVarChar, 10)
            .Value = "EquipSel";      

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new EquipmentLookupModel
            {
                EquAutoID =
                    Convert.ToDecimal(reader["EquAutoID"]),

                EqupType =
                    reader["EqupType"]?.ToString() ?? "",

                EqupID =
                    reader["EqupID"]?.ToString() ?? ""
            });
        }

        return result;
    }

    // =========================================================
    // COMPONENT SELECT
    // =========================================================
    //
    // This uses the existing legacy view logic:
    // VW_Componentview
    //
    // If you already have a component SELECT SP,
    // replace only this SQL with that SP.
    // =========================================================

    public async Task<List<ComponentModel>> GetComponentsAsync(
        decimal companyId)
    {
        var result = new List<ComponentModel>();

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
       
        using SqlCommand cmd = new("sp_Get_tbl_Val", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.Add("@id", SqlDbType.Decimal)
            .Value = companyId;

        cmd.Parameters.Add("@tblflg", SqlDbType.NVarChar, 10)
            .Value = "CompGrid";

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ComponentModel
            {
                CompAutoID = GetDecimal(reader, "CompAutoID"),

                ProcessAreaID =
                    GetDecimal(reader, "ProcessAreaID"),

                ProcessArea =
                    GetString(reader, "ProcessArea"),

                EqupID =
                    GetDecimal(reader, "EqupID"),

                EquipmentID =
                    GetString(reader, "EquipID"),

                CompNo =
                    GetString(reader, "CompNo"),

                CompName =
                    GetString(reader, "CompName"),

                Insulated =
                    GetString(reader, "Insulated"),

                Painting =
                    GetString(reader, "Painting"),

                Materialtype =
                    GetString(reader, "Materialtype"),

                MaterialSpecification =
                    GetString(reader, "materialspecification"),

                NormalThickness =
                    GetString(reader, "NormalThickness"),

                ConstThickness =
                    GetString(reader, "ConstThickness"),

                MRT =
                    GetDecimal(reader, "MRT"),

                Designpressure =
                    GetString(reader, "Designpressure"),

                DesignTemp =
                    GetString(reader, "DesignTemp"),

                OPPressure =
                    GetString(reader, "OPPressure"),

                OPTemp =
                    GetString(reader, "OPTemp"),

                CorrosionAllownce =
                    GetString(reader, "CorrosionAllownce"),

                InspectionEffective =
                    GetString(reader, "InspectionEffective"),

                ExpectedRate =
                    GetDecimal(reader, "ExpectedRate"),

                NoofInspection =
                    Convert.ToInt32(
                        GetDecimal(reader, "NoofInspection")),

                Clad =
                    GetString(reader, "Clad"),

                Defaultvalue =
                    GetDecimal(reader, "Defaultvalue"),

                CompanyID =
                    companyId
            });
        }

        return result;
    }

    // =========================================================
    // SAVE / UPDATE
    // =========================================================

    public async Task SaveComponentAsync(
        ComponentModel model,
        decimal userId,
        string saveFlag)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd =
            new SqlCommand("sp_Component_Save", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.AddWithValue("@idp",
            model.CompAutoID);

        cmd.Parameters.AddWithValue("@ProcessareaIDp",
            model.ProcessAreaID);

        cmd.Parameters.AddWithValue("@EqupIDp",
            model.EqupID);

        cmd.Parameters.AddWithValue("@CompNop",
            model.CompNo ?? "");

        cmd.Parameters.AddWithValue("@CompNamep",
            model.CompName ?? "");

        cmd.Parameters.AddWithValue("@Insulatedp",
            model.Insulated ?? "");

        cmd.Parameters.AddWithValue("@Paintingp",
            model.Painting ?? "");

        cmd.Parameters.AddWithValue("@Materialtypeg",
            model.Materialtype ?? "");

        cmd.Parameters.AddWithValue("@materialspecificationp",
            model.MaterialSpecification ?? "");

        cmd.Parameters.AddWithValue("@NormalThicknessp",
            model.NormalThickness ?? "");

        cmd.Parameters.AddWithValue("@ConstThicknessp",
            model.ConstThickness ?? "");

        cmd.Parameters.AddWithValue("@MRTp",
            model.MRT);

        cmd.Parameters.AddWithValue("@Designpressurep",
            model.Designpressure ?? "");

        cmd.Parameters.AddWithValue("@DesignTempp",
            model.DesignTemp ?? "");

        cmd.Parameters.AddWithValue("@OPPressurep",
            model.OPPressure ?? "");

        cmd.Parameters.AddWithValue("@OPTempp",
            model.OPTemp ?? "");

        cmd.Parameters.AddWithValue("@CorrosionAllowncep",
            model.CorrosionAllownce ?? "");

        cmd.Parameters.AddWithValue("@InspectionEffectivep",
            model.InspectionEffective ?? "");

        cmd.Parameters.AddWithValue("@ExpectedRatep",
            model.ExpectedRate);

        cmd.Parameters.AddWithValue("@NoofInspectionp",
            model.NoofInspection);

        cmd.Parameters.AddWithValue("@cladp",
            model.Clad ?? "");

        cmd.Parameters.AddWithValue("@Defaultvalue",
            model.Defaultvalue);

        cmd.Parameters.AddWithValue("@CompanyID",
            model.CompanyID);

        cmd.Parameters.AddWithValue("@useridp",
            userId);

        cmd.Parameters.AddWithValue("@saveflagp",
            saveFlag);

        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================
    // DELETE
    // =========================================================

    public async Task DeleteComponentAsync(
        decimal componentId,
        decimal companyId,
        decimal userId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd =
            new SqlCommand("sp_Component_Save", conn);

        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.AddWithValue("@idp",
            componentId);

        cmd.Parameters.AddWithValue("@ProcessareaIDp", 0);
        cmd.Parameters.AddWithValue("@EqupIDp", "");
        cmd.Parameters.AddWithValue("@CompNop", "");
        cmd.Parameters.AddWithValue("@CompNamep", "");
        cmd.Parameters.AddWithValue("@Insulatedp", "");
        cmd.Parameters.AddWithValue("@Paintingp", "");
        cmd.Parameters.AddWithValue("@Materialtypeg", "");
        cmd.Parameters.AddWithValue("@materialspecificationp", "");
        cmd.Parameters.AddWithValue("@NormalThicknessp", "");
        cmd.Parameters.AddWithValue("@ConstThicknessp", "");
        cmd.Parameters.AddWithValue("@MRTp", 0);
        cmd.Parameters.AddWithValue("@Designpressurep", "");
        cmd.Parameters.AddWithValue("@DesignTempp", "");
        cmd.Parameters.AddWithValue("@OPPressurep", "");
        cmd.Parameters.AddWithValue("@OPTempp", "");
        cmd.Parameters.AddWithValue("@CorrosionAllowncep", "");
        cmd.Parameters.AddWithValue("@InspectionEffectivep", "");
        cmd.Parameters.AddWithValue("@ExpectedRatep", 0);
        cmd.Parameters.AddWithValue("@NoofInspectionp", 0);
        cmd.Parameters.AddWithValue("@cladp", "");
        cmd.Parameters.AddWithValue("@Defaultvalue", 0);
        cmd.Parameters.AddWithValue("@CompanyID", companyId);
        cmd.Parameters.AddWithValue("@useridp", userId);

        // Existing SP handles delete
        cmd.Parameters.AddWithValue("@saveflagp", "D");

        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================
    // SAFE READER HELPERS
    // =========================================================

    private static string GetString(
        SqlDataReader reader,
        string column)
    {
        try
        {
            return reader[column] == DBNull.Value
                ? ""
                : reader[column]?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static decimal GetDecimal(
        SqlDataReader reader,
        string column)
    {
        try
        {
            if (reader[column] == DBNull.Value)
                return 0;

            return Convert.ToDecimal(reader[column]);
        }
        catch
        {
            return 0;
        }
    }
}

// =========================================================
// EQUIPMENT LOOKUP
// =========================================================

public class EquipmentLookupModel
{
    public decimal EquAutoID { get; set; }

    public string EqupType { get; set; } = "";

    public string EqupID { get; set; } = "";
}