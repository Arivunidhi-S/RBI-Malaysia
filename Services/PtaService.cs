using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class PtaService
{
    public record Option(string Code, string Label);

    private readonly string _cs;
    private readonly SulfidationService _lookups;   // reuse existing Process Area / Equipment / Component lookups
    private readonly UserSession _session;

    public PtaService(IConfiguration config, SulfidationService lookups, UserSession session)
    {
        // Same key as ExCUIService / the other migrated services; DefaultConnection kept as a fallback.
        _cs = config.GetConnectionString("connString")
              ?? config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string missing.");
        _lookups = lookups;
        _session = session;
    }

    // Code = Material value in Ref_PTA. Labels are exactly what the legacy page stored in PTA.Material.
    public List<Option> MaterialOptions { get; } = new()
    {
        new("Alloy600", "All Regular 300Series SS & Alloy 600 & 800"),
        new("HGrade",   "H Grade 300series SS"),
        new("LGrade",   "L Grade 300series SS"),
        new("321SS",    "321 Stainless Steel"),
        new("347SS",    "347 SS Alloy 20,Alloy 625,All austenitic weld overlay"),
    };

    // Code = column name in Ref_PTA. Labels are exactly what the legacy page stored in PTA.Heat.
    public List<Option> HeatOptions { get; } = new()
    {
        new("SolAnn",     "Solution Annealed"),
        new("BeforeWeld", "Stablized Before Welding"),
        new("AfterWeld",  "Stablized After Welding"),
    };

    private static readonly string[] InsEffColumns = { "A", "B", "C", "D", "E" };

    // PTA.Material / PTA.Heat are nvarchar(50) in the legacy table.
    private const int LabelMax = 50;

    // ---- Shared lookups (delegated) ----
    public Task<List<ProcessAreaModel>> GetProcessAreasAsync(object companyId) =>
        _lookups.GetProcessAreasAsync((dynamic)companyId);
    public Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId) =>
        _lookups.GetEquipmentsByProcessAsync(processAreaId);
    public Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId) =>
        _lookups.GetComponentsByEquipmentAsync(equipmentId);

    // Legacy: optemp < 427 -> "LT", otherwise "GT"
    public static string TempBand(double opTemp) => opTemp < 427 ? "LT" : "GT";

    // Operating temperature of the component (legacy: Attributes["OPTemp"]), from Tbl_EquipmentComponentDetails.
    public Task<double?> GetOpTempAsync(decimal compAutoId) => OpTempLookup.GetAsync(_cs, compAutoId);

    // ---- Material + Heat + operating temperature -> Svi (legacy OnSelectedIndexChanged_PTA) ----
    public async Task<string> ComputeSviAsync(string materialCode, string heatCode, decimal compAutoId)
    {
        if (!MaterialOptions.Exists(o => o.Code == materialCode)) throw new ArgumentException("Invalid material.");
        if (!HeatOptions.Exists(o => o.Code == heatCode)) throw new ArgumentException("Invalid heat treatment.");

        double opTemp = await OpTempLookup.GetAsync(_cs, compAutoId)
                        ?? throw new InvalidOperationException("Operating Temperature not found for this component.");

        // Column name comes from the whitelist above, never from raw input.
        var sql = $"SELECT [{heatCode}] FROM Ref_PTA WHERE Material = @mat AND temp = @temp";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@mat", materialCode);
        cmd.Parameters.AddWithValue("@temp", TempBand(opTemp));
        var result = await cmd.ExecuteScalarAsync();
        var svi = result == null || result == DBNull.Value ? "" : Convert.ToString(result)!.Trim();
        if (svi.Length == 0) throw new InvalidOperationException("No susceptibility (Svi) found for the selected combination.");
        return svi;
    }

    // PTA uses a different scale from HIC: High 5000 / Medium 500 / Low 50 / None 1
    private static int SviToValue(string? svi) => svi?.Trim() switch
    {
        "High"   => 5000,
        "Medium" => 500,
        "Low"    => 50,
        "None"   => 1,
        _ => throw new InvalidOperationException("Svi must be High, Medium, Low or None.")
    };

    // ---- Calculate (legacy btn_PTA_Calc_Click) : Df = base * Age^1.1 ----
    public async Task<PtaModel> CalculatePtaAsync(PtaModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.PtaAge is null || string.IsNullOrEmpty(m.PtaInsEff)
            || string.IsNullOrEmpty(m.PtaNofIns) || string.IsNullOrEmpty(m.PtaSvi))
            throw new InvalidOperationException("Please check all the fields.");

        if (!InsEffColumns.Contains(m.PtaInsEff)) throw new InvalidOperationException("Invalid inspection effectiveness.");
        if (!int.TryParse(m.PtaNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");

        m.PtaSviVal = SviToValue(m.PtaSvi);

        var sql = $"SELECT [{m.PtaInsEff}] FROM Ref_SCC " +
                  "WHERE TRY_CAST(Inspection AS int) = @n AND TRY_CAST(Svi AS int) = @svi";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", nIns);
        cmd.Parameters.AddWithValue("@svi", m.PtaSviVal);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value
            || !double.TryParse(Convert.ToString(result), NumberStyles.Float, CultureInfo.InvariantCulture, out var baseDf))
            throw new InvalidOperationException("No base damage factor found in Ref_SCC for the selected inputs.");

        var df = baseDf * Math.Pow(m.PtaAge.Value, 1.1);
        m.PtaDf = Math.Round(df, 3).ToString(CultureInfo.InvariantCulture);
        return m;
    }

    // ---- Persistence: dbo.PTA (what sp_Carbanate_Save wrote for flags N3 / U3) ----
    private static string? Label(List<Option> list, string? code) =>
        list.Find(o => o.Code == code)?.Label;

    // The table stores labels only, so rebuild the dropdown code from the label.
    // Fallback: a label cut at 50 characters by the old column still matches by prefix.
    private static string? CodeFor(List<Option> list, string? label)
    {
        label = label?.Trim();
        if (string.IsNullOrEmpty(label)) return null;
        return (list.Find(o => string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase))
                ?? list.Find(o => o.Label.StartsWith(label, StringComparison.OrdinalIgnoreCase)))?.Code;
    }

    private decimal? CurrentUserId() =>
        decimal.TryParse(Convert.ToString(_session.UserID, CultureInfo.InvariantCulture),
                         NumberStyles.Number, CultureInfo.InvariantCulture, out var id) ? id : null;

    public async Task<PtaModel> SavePtaAsync(PtaModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.PtaAge is null || string.IsNullOrEmpty(m.PtaInsEff) || string.IsNullOrEmpty(m.PtaNofIns)
            || string.IsNullOrEmpty(m.PtaSvi) || string.IsNullOrEmpty(m.PtaDf))
            throw new InvalidOperationException("Please check all the fields (and run Calculate first).");

        if (!int.TryParse(m.PtaNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");
        if (!decimal.TryParse(m.PtaDf, NumberStyles.Number, CultureInfo.InvariantCulture, out var df))
            throw new InvalidOperationException("Df value is invalid. Run Calculate again.");
        m.PtaSviVal = SviToValue(m.PtaSvi);

        m.Material ??= Label(MaterialOptions, m.MaterialCode);
        m.Heat     ??= Label(HeatOptions, m.HeatCode);

        if ((m.Material?.Length ?? 0) > LabelMax)
            throw new InvalidOperationException(
                $"The material text is longer than {LabelMax} characters, the size of PTA.Material. " +
                "Run the ALTER COLUMN script (nvarchar(100)) or shorten the label.");

        // One active row per Process/Equipment/Component: update it if present (legacy "U3"), else insert ("N3").
        const string sql = @"
DECLARE @id numeric(18,0) = (SELECT TOP 1 PTAID FROM dbo.PTA
                             WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
                             ORDER BY PTAID DESC);
IF @id IS NULL
BEGIN
    INSERT INTO dbo.PTA
        (ProcID, EquID, CompID, PTAAge, PTAInsEff, PTAnofIns, InspectDate, PTASvi, PTASviVal, PTADf,
         Material, Heat, CreatedBy, CreatedDate, Rowversions, Deleted)
    VALUES
        (@p, @e, @c, @age, @ins, @nins, @date, @svi, @sviVal, @df,
         @mat, @heat, @uid, GETDATE(), 0, 0);
    SELECT CAST(SCOPE_IDENTITY() AS numeric(18,0));
END
ELSE
BEGIN
    UPDATE dbo.PTA SET
        PTAAge=@age, PTAInsEff=@ins, PTAnofIns=@nins, InspectDate=@date, PTASvi=@svi, PTASviVal=@sviVal, PTADf=@df,
        Material=@mat, Heat=@heat,
        ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
    WHERE PTAID=@id;
    SELECT @id;
END";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, m.ProcID, m.EquID, m.CompID);
        cmd.Parameters.AddWithValue("@age",    (decimal)m.PtaAge.Value);
        cmd.Parameters.AddWithValue("@ins",    m.PtaInsEff);
        cmd.Parameters.AddWithValue("@nins",   (decimal)nIns);
        cmd.Parameters.AddWithValue("@date",   m.InspectDate ?? DateTime.Today);
        cmd.Parameters.AddWithValue("@svi",    m.PtaSvi);
        cmd.Parameters.AddWithValue("@sviVal", (decimal)m.PtaSviVal);
        cmd.Parameters.AddWithValue("@df",     df);
        cmd.Parameters.AddWithValue("@mat",    (object?)m.Material ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@heat",   (object?)m.Heat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uid",    (object?)CurrentUserId() ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        if (id != null && id != DBNull.Value) m.PtaID = Convert.ToDecimal(id, CultureInfo.InvariantCulture);

        // TODO: legacy also called InspectionPlan(proc, equip, comp, "PTA", insEff, inspectDate) after save.
        //       Hook up the Blazor equivalent here, using THIS page's InsEff / InspectDate.
        return m;
    }

    public async Task<PtaModel?> GetPtaRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
SELECT TOP 1 PTAID, PTAAge, PTAInsEff, PTAnofIns, InspectDate, PTASvi, PTASviVal, PTADf, Material, Heat
FROM dbo.PTA
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
ORDER BY PTAID DESC";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? S(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToString(o)?.Trim(); }
        decimal? D(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToDecimal(o, CultureInfo.InvariantCulture); }

        var m = new PtaModel
        {
            PtaID = D("PTAID") ?? 0, ProcID = procId, EquID = equId, CompID = compId,
            PtaInsEff = S("PTAInsEff"),
            PtaNofIns = D("PTAnofIns") is { } n ? ((int)n).ToString(CultureInfo.InvariantCulture) : null,
            PtaSvi = S("PTASvi"),
            PtaSviVal = D("PTASviVal") is { } sv ? (int)sv : 0,
            PtaDf = D("PTADf") is { } df ? df.ToString("0.###", CultureInfo.InvariantCulture) : null,
            Material = S("Material"), Heat = S("Heat"),
            MaterialCode = CodeFor(MaterialOptions, S("Material")),
            HeatCode = CodeFor(HeatOptions, S("Heat")),
            KnownCrack = false   // not stored in dbo.PTA
        };
        if (D("PTAAge") is { } age) m.PtaAge = (int)age;
        if (r["InspectDate"] != DBNull.Value) m.InspectDate = Convert.ToDateTime(r["InspectDate"]);
        return m;
    }

    // Soft delete, using the table's own Deleted flag.
    public async Task DeletePtaAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
UPDATE dbo.PTA SET Deleted=1, ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        cmd.Parameters.AddWithValue("@uid", (object?)CurrentUserId() ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddKeys(SqlCommand cmd, decimal procId, decimal equId, decimal compId)
    {
        cmd.Parameters.AddWithValue("@p", procId);
        cmd.Parameters.AddWithValue("@e", equId);
        cmd.Parameters.AddWithValue("@c", compId);
    }
}
