using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class HicSohicService
{
    public record Option(string Code, string Label);

    private readonly string _cs;
    private readonly SulfidationService _lookups;   // reuse existing Process Area / Equipment / Component lookups
    private readonly UserSession _session;

    public HicSohicService(IConfiguration config, SulfidationService lookups, UserSession session)
    {
        // Same key as ExCUIService / the other migrated services; DefaultConnection kept as a fallback.
        _cs = config.GetConnectionString("connString")
              ?? config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string missing.");
        _lookups = lookups;
        _session = session;
    }

    // ---- Option lists. Codes are the DB keys / column names used by the legacy queries ----
    public List<Option> PHOptions { get; } = new()
    {
        new("5.5", "< 5.5"),
        new("7.5", "5.5 to 7.5"),
        new("7.6", "7.6 to 8.3"),
        new("8.4", "8.4 to 8.9"),
        new("9.0", "> 9.0"),
    };

    // Code = column name in Ref_Envi_Sev
    public List<Option> H2SOptions { get; } = new()
    {
        new("Fiftyppm",   "< 55 ppm"),
        new("Thosandppm", "50 to 1000 ppm"),
        new("Tenppm",     "1000 to 10000 ppm"),
        new("GtTenppm",   "> 10000 ppm"),
    };

    // Code = column name in Ref_HIC
    public List<Option> HeatOptions { get; } = new()
    {
        new("weld", "As-Welded"),
        new("pwht", "PWHT"),
    };

    // Code = stlsul value in Ref_HIC
    public List<Option> SteelSulfurOptions { get; } = new()
    {
        new("1", "High Sulfar Steel"),
        new("2", "Low Sulfar Steel"),
        new("3", "Ultra Sulfar Steel"),
    };

    private static readonly string[] InsEffColumns = { "A", "B", "C", "D", "E" };

    // ---- Shared lookups (delegated) ----
    public Task<List<ProcessAreaModel>> GetProcessAreasAsync(object companyId) =>
        _lookups.GetProcessAreasAsync((dynamic)companyId);
    public Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId) =>
        _lookups.GetEquipmentsByProcessAsync(processAreaId);
    public Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId) =>
        _lookups.GetComponentsByEquipmentAsync(equipmentId);

    // ---- STEP 1: pH + H2S -> Environmental Severity (legacy OnSelectedIndexChanged_HIC) ----
    public async Task<string> ComputeEnvSeverityAsync(string h2sCode, string phCode)
    {
        if (!H2SOptions.Exists(o => o.Code == h2sCode)) throw new ArgumentException("Invalid H2S content.");
        if (!decimal.TryParse(phCode, NumberStyles.Number, CultureInfo.InvariantCulture, out var ph))
            throw new ArgumentException("Invalid pH value.");

        // Column name comes from the whitelist above, never from raw input.
        var sql = $"SELECT [{h2sCode}] FROM Ref_Envi_Sev WHERE TRY_CAST(pHofWater AS decimal(4,1)) = @ph";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ph", ph);
        var result = await cmd.ExecuteScalarAsync();
        var sev = result == null || result == DBNull.Value ? "" : Convert.ToString(result)!.Trim();
        if (sev.Length == 0) throw new InvalidOperationException("No environmental severity found for the selected pH / H2S.");
        return sev;
    }

    // ---- STEP 2: Heat + Steel Sulfur + Severity -> Svi (legacy OnSelectedIndexChanged_SteelSul) ----
    public async Task<string> ComputeSviAsync(string steelCode, string heatCode, string severity)
    {
        if (!HeatOptions.Exists(o => o.Code == heatCode)) throw new ArgumentException("Invalid heat treatment.");
        if (!SteelSulfurOptions.Exists(o => o.Code == steelCode)) throw new ArgumentException("Invalid steel sulfur.");

        var sql = $"SELECT [{heatCode}] FROM Ref_HIC WHERE stlsul = @stl AND Envi = @env";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@stl", steelCode);
        cmd.Parameters.AddWithValue("@env", severity.Trim());
        var result = await cmd.ExecuteScalarAsync();
        var svi = result == null || result == DBNull.Value ? "" : Convert.ToString(result)!.Trim();
        if (svi.Length == 0) throw new InvalidOperationException("No susceptibility (Svi) found for the selected combination.");
        return svi;
    }

    private static int SviToValue(string? svi) => svi?.Trim() switch
    {
        "High"   => 100,
        "Medium" => 10,
        "Low"    => 1,
        "None"   => 1,
        _ => throw new InvalidOperationException("Svi must be High, Medium, Low or None.")
    };

    // ---- Calculate (legacy btn_HIC_Calc_Click) : Df = base * Age^1.1 ----
    public async Task<HicSohicModel> CalculateHicAsync(HicSohicModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HicAge is null || string.IsNullOrEmpty(m.HicInsEff)
            || string.IsNullOrEmpty(m.HicnofIns) || string.IsNullOrEmpty(m.HicSvi))
            throw new InvalidOperationException("Please check all the fields.");

        if (!InsEffColumns.Contains(m.HicInsEff)) throw new InvalidOperationException("Invalid inspection effectiveness.");
        if (!int.TryParse(m.HicnofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");

        m.HicSviVal = SviToValue(m.HicSvi);

        var sql = $"SELECT [{m.HicInsEff}] FROM Ref_SCC " +
                  "WHERE TRY_CAST(Inspection AS int) = @n AND TRY_CAST(Svi AS int) = @svi";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", nIns);
        cmd.Parameters.AddWithValue("@svi", m.HicSviVal);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value
            || !double.TryParse(Convert.ToString(result), NumberStyles.Float, CultureInfo.InvariantCulture, out var baseDf))
            throw new InvalidOperationException("No base damage factor found in Ref_SCC for the selected inputs.");

        var df = baseDf * Math.Pow(m.HicAge.Value, 1.1);
        m.HicDfSOHIC = Math.Round(df, 3).ToString(CultureInfo.InvariantCulture);
        return m;
    }

    // ---- Persistence: dbo.HIC (legacy table, same columns as SulfideCrackingSave wrote) ----
    private static string? Label(List<Option> list, string? code) =>
        list.Find(o => o.Code == code)?.Label;
    private static string? CodeFor(List<Option> list, string? label) =>
        list.Find(o => string.Equals(o.Label, label?.Trim(), StringComparison.OrdinalIgnoreCase))?.Code;

    private decimal? CurrentUserId() =>
        decimal.TryParse(Convert.ToString(_session.UserID, CultureInfo.InvariantCulture),
                         NumberStyles.Number, CultureInfo.InvariantCulture, out var id) ? id : null;

    public async Task<HicSohicModel> SaveHicAsync(HicSohicModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HicAge is null || string.IsNullOrEmpty(m.HicInsEff) || string.IsNullOrEmpty(m.HicnofIns)
            || string.IsNullOrEmpty(m.HicSvi) || string.IsNullOrEmpty(m.HicDfSOHIC))
            throw new InvalidOperationException("Please check all the fields (and run Calculate first).");

        if (!int.TryParse(m.HicnofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");
        if (!decimal.TryParse(m.HicDfSOHIC, NumberStyles.Number, CultureInfo.InvariantCulture, out var df))
            throw new InvalidOperationException("Df value is invalid. Run Calculate again.");
        m.HicSviVal = SviToValue(m.HicSvi);

        // Labels are what the legacy table stores; rebuild from codes if the page didn't set them.
        m.PHwater     ??= Label(PHOptions, m.PHwaterCode);
        m.H2S         ??= Label(H2SOptions, m.H2SCode);
        m.Heat        ??= Label(HeatOptions, m.HeatCode);
        m.SteelSulfur ??= Label(SteelSulfurOptions, m.SteelSulfurCode);

        // One active row per Process/Equipment/Component: update it if present (legacy "U1"), else insert ("N1").
        const string sql = @"
DECLARE @id numeric(18,0) = (SELECT TOP 1 HICID FROM dbo.HIC
                             WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
                             ORDER BY HICID DESC);
IF @id IS NULL
BEGIN
    INSERT INTO dbo.HIC
        (ProcID, EquID, CompID, HICAge, HICInsEff, HICnofIns, InspectDate, HICSvi, HICSviVal, HICDf,
         pHwater, SteelSul, Heat, steelsulf, Severity, CreatedBy, CreatedDate, Rowversions, Deleted)
    VALUES
        (@p, @e, @c, @age, @ins, @nins, @date, @svi, @sviVal, @df,
         @ph, @h2s, @heat, @st, @sev, @uid, GETDATE(), 0, 0);
    SELECT CAST(SCOPE_IDENTITY() AS numeric(18,0));
END
ELSE
BEGIN
    UPDATE dbo.HIC SET
        HICAge=@age, HICInsEff=@ins, HICnofIns=@nins, InspectDate=@date, HICSvi=@svi, HICSviVal=@sviVal, HICDf=@df,
        pHwater=@ph, SteelSul=@h2s, Heat=@heat, steelsulf=@st, Severity=@sev,
        ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
    WHERE HICID=@id;
    SELECT @id;
END";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, m.ProcID, m.EquID, m.CompID);
        cmd.Parameters.AddWithValue("@age",    (decimal)m.HicAge.Value);
        cmd.Parameters.AddWithValue("@ins",    m.HicInsEff);
        cmd.Parameters.AddWithValue("@nins",   (decimal)nIns);
        cmd.Parameters.AddWithValue("@date",   m.InspectDate ?? DateTime.Today);
        cmd.Parameters.AddWithValue("@svi",    m.HicSvi);
        cmd.Parameters.AddWithValue("@sviVal", (decimal)m.HicSviVal);
        cmd.Parameters.AddWithValue("@df",     df);
        cmd.Parameters.AddWithValue("@ph",     (object?)m.PHwater ?? DBNull.Value);       // pHwater
        cmd.Parameters.AddWithValue("@h2s",    (object?)m.H2S ?? DBNull.Value);           // SteelSul (legacy name for the ppm combo)
        cmd.Parameters.AddWithValue("@heat",   (object?)m.Heat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@st",     (object?)m.SteelSulfur ?? DBNull.Value);   // steelsulf
        cmd.Parameters.AddWithValue("@sev",    (object?)m.Severity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uid",    (object?)CurrentUserId() ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        if (id != null && id != DBNull.Value) m.HicID = Convert.ToDecimal(id, CultureInfo.InvariantCulture);

        // TODO: legacy also called InspectionPlan(proc, equip, comp, "H2S", insEff, inspectDate) after save.
        //       Hook up the Blazor equivalent here, using THIS page's InsEff / InspectDate.
        return m;
    }

    public async Task<HicSohicModel?> GetHicRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
SELECT TOP 1 HICID, HICAge, HICInsEff, HICnofIns, InspectDate, HICSvi, HICSviVal, HICDf,
             pHwater, SteelSul, Heat, steelsulf, Severity
FROM dbo.HIC
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
ORDER BY HICID DESC";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? S(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToString(o)?.Trim(); }
        decimal? D(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToDecimal(o, CultureInfo.InvariantCulture); }

        var m = new HicSohicModel
        {
            HicID = D("HICID") ?? 0, ProcID = procId, EquID = equId, CompID = compId,
            HicInsEff = S("HICInsEff"),
            HicnofIns = D("HICnofIns") is { } n ? ((int)n).ToString(CultureInfo.InvariantCulture) : null,
            HicSvi = S("HICSvi"),
            HicSviVal = D("HICSviVal") is { } sv ? (int)sv : 0,
            HicDfSOHIC = D("HICDf") is { } df ? df.ToString("0.###", CultureInfo.InvariantCulture) : null,
            PHwater = S("pHwater"), H2S = S("SteelSul"), Heat = S("Heat"), SteelSulfur = S("steelsulf"),
            Severity = S("Severity"),
            // The table stores labels only; rebuild the dropdown codes from them.
            PHwaterCode = CodeFor(PHOptions, S("pHwater")),
            H2SCode = CodeFor(H2SOptions, S("SteelSul")),
            HeatCode = CodeFor(HeatOptions, S("Heat")),
            SteelSulfurCode = CodeFor(SteelSulfurOptions, S("steelsulf")),
            KnownCrack = false   // not stored in dbo.HIC
        };
        if (D("HICAge") is { } age) m.HicAge = (int)age;
        if (r["InspectDate"] != DBNull.Value) m.InspectDate = Convert.ToDateTime(r["InspectDate"]);
        return m;
    }

    // Soft delete, using the table's own Deleted flag.
    public async Task DeleteHicAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
UPDATE dbo.HIC SET Deleted=1, ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
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
