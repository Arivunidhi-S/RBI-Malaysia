using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class ClsccService
{
    public record Option(string Code, string Label);

    private readonly string _cs;
    private readonly SulfidationService _lookups;   // reuse existing Process Area / Equipment / Component lookups
    private readonly UserSession _session;

    public ClsccService(IConfiguration config, SulfidationService lookups, UserSession session)
    {
        _cs = config.GetConnectionString("connString")
              ?? config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string missing.");
        _lookups = lookups;
        _session = session;
    }

    // Code = pH value in Ref_CLSCC. Labels are exactly what the legacy page stored in CLSCC.pH.
    public List<Option> PHOptions { get; } = new()
    {
        new("LT", "pH <= 10"),
        new("GT", "pH > 10"),
    };

    // Code = column name in Ref_CLSCC. Labels are exactly what the legacy page stored in CLSCC.Chldppm.
    public List<Option> ChlorideOptions { get; } = new()
    {
        new("oneten",       "1-10"),
        new("elhund",       "11-100"),
        new("hundthousand", "101-1000"),
        new("gtthousand",   ">1000"),
    };

    private static readonly string[] InsEffColumns = { "A", "B", "C", "D", "E" };

    // ---- Shared lookups (delegated) ----
    public Task<List<ProcessAreaModel>> GetProcessAreasAsync(object companyId) =>
        _lookups.GetProcessAreasAsync((dynamic)companyId);
    public Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId) =>
        _lookups.GetEquipmentsByProcessAsync(processAreaId);
    public Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId) =>
        _lookups.GetComponentsByEquipmentAsync(equipmentId);

    // Operating temperature of the component (legacy: Attributes["OPTemp"]), from Tbl_EquipmentComponentDetails.
    public Task<double?> GetOpTempAsync(decimal compAutoId) => OpTempLookup.GetAsync(_cs, compAutoId);

    // Legacy temperature bucket used as "temp" in Ref_CLSCC (ported exactly, including its fall-through).
    public static int TempBucket(string phCode, double t)
    {
        if (phCode == "LT")
        {
            if (t > 38 && t < 66)   return 38;
            if (t >= 66 && t < 93)  return 66;
            if (t >= 93 && t < 149) return 93;
            if (t < 38)             return 38;
            return 93;              // legacy "else": exactly 38, or 149 and above
        }
        // "GT"
        if (t < 93) return 93;
        return 149;                 // 93 to <149 and >=149 both map to 149 in legacy
    }

    // ---- pH + Chloride + operating temperature -> Svi (legacy OnSelectedIndexChanged_CLS) ----
    public async Task<string> ComputeSviAsync(string phCode, string chlorideCode, decimal compAutoId)
    {
        if (!PHOptions.Exists(o => o.Code == phCode)) throw new ArgumentException("Invalid pH.");
        if (!ChlorideOptions.Exists(o => o.Code == chlorideCode)) throw new ArgumentException("Invalid chloride range.");

        double opTemp = await OpTempLookup.GetAsync(_cs, compAutoId)
                        ?? throw new InvalidOperationException("Operating Temperature not found for this component.");
        int bucket = TempBucket(phCode, opTemp);

        // Column name comes from the whitelist above, never from raw input.
        var sql = $"SELECT [{chlorideCode}] FROM Ref_CLSCC WHERE TRY_CAST(temp AS int) = @t AND pH = @ph";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@t", bucket);
        cmd.Parameters.AddWithValue("@ph", phCode);
        var result = await cmd.ExecuteScalarAsync();
        var svi = result == null || result == DBNull.Value ? "" : Convert.ToString(result)!.Trim();
        if (svi.Length == 0) throw new InvalidOperationException("No susceptibility (Svi) found for the selected combination.");
        return svi;
    }

    // Same scale as PTA: High 5000 / Medium 500 / Low 50 / None 1
    private static int SviToValue(string? svi) => svi?.Trim() switch
    {
        "High"   => 5000,
        "Medium" => 500,
        "Low"    => 50,
        "None"   => 1,
        _ => throw new InvalidOperationException("Svi must be High, Medium, Low or None.")
    };

    // ---- Calculate (legacy btn_CLS_Calc_Click) : Df = base * Age^1.1 ----
    public async Task<ClsccModel> CalculateClsAsync(ClsccModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.ClsAge is null || string.IsNullOrEmpty(m.ClsInsEff)
            || string.IsNullOrEmpty(m.ClsNofIns) || string.IsNullOrEmpty(m.ClsSvi))
            throw new InvalidOperationException("Please check all the fields.");

        if (!InsEffColumns.Contains(m.ClsInsEff)) throw new InvalidOperationException("Invalid inspection effectiveness.");
        if (!int.TryParse(m.ClsNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");

        m.ClsSviVal = SviToValue(m.ClsSvi);

        var sql = $"SELECT [{m.ClsInsEff}] FROM Ref_SCC " +
                  "WHERE TRY_CAST(Inspection AS int) = @n AND TRY_CAST(Svi AS int) = @svi";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", nIns);
        cmd.Parameters.AddWithValue("@svi", m.ClsSviVal);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value
            || !double.TryParse(Convert.ToString(result), NumberStyles.Float, CultureInfo.InvariantCulture, out var baseDf))
            throw new InvalidOperationException("No base damage factor found in Ref_SCC for the selected inputs.");

        var df = baseDf * Math.Pow(m.ClsAge.Value, 1.1);
        m.ClsDf = Math.Round(df, 3).ToString(CultureInfo.InvariantCulture);
        return m;
    }

    // ---- Persistence: dbo.CLSCC (what sp_Carbanate_Save wrote for flags N4 / U4) ----
    private static string? Label(List<Option> list, string? code) =>
        list.Find(o => o.Code == code)?.Label;

    // The table stores labels only, so rebuild the dropdown code from the label.
    private static string? CodeFor(List<Option> list, string? label)
    {
        label = label?.Trim();
        if (string.IsNullOrEmpty(label)) return null;
        return list.Find(o => string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private decimal? CurrentUserId() =>
        decimal.TryParse(Convert.ToString(_session.UserID, CultureInfo.InvariantCulture),
                         NumberStyles.Number, CultureInfo.InvariantCulture, out var id) ? id : null;

    public async Task<ClsccModel> SaveClsAsync(ClsccModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.ClsAge is null || string.IsNullOrEmpty(m.ClsInsEff) || string.IsNullOrEmpty(m.ClsNofIns)
            || string.IsNullOrEmpty(m.ClsSvi) || string.IsNullOrEmpty(m.ClsDf))
            throw new InvalidOperationException("Please check all the fields (and run Calculate first).");

        if (!int.TryParse(m.ClsNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");
        if (!decimal.TryParse(m.ClsDf, NumberStyles.Number, CultureInfo.InvariantCulture, out var df))
            throw new InvalidOperationException("Df value is invalid. Run Calculate again.");
        m.ClsSviVal = SviToValue(m.ClsSvi);

        m.PH       ??= Label(PHOptions, m.PHCode);
        m.Chloride ??= Label(ChlorideOptions, m.ChlorideCode);

        // One active row per Process/Equipment/Component: update it if present (legacy "U4"), else insert ("N4").
        const string sql = @"
DECLARE @id numeric(18,0) = (SELECT TOP 1 CLSID FROM dbo.CLSCC
                             WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
                             ORDER BY CLSID DESC);
IF @id IS NULL
BEGIN
    INSERT INTO dbo.CLSCC
        (ProcID, EquID, CompID, CLSAge, CLSInsEff, CLSnofIns, InspectDate, CLSSvi, CLSSviVal, CLSDf,
         pH, Chldppm, CreatedBy, CreatedDate, Rowversions, Deleted)
    VALUES
        (@p, @e, @c, @age, @ins, @nins, @date, @svi, @sviVal, @df,
         @ph, @chl, @uid, GETDATE(), 0, 0);
    SELECT CAST(SCOPE_IDENTITY() AS numeric(18,0));
END
ELSE
BEGIN
    UPDATE dbo.CLSCC SET
        CLSAge=@age, CLSInsEff=@ins, CLSnofIns=@nins, InspectDate=@date, CLSSvi=@svi, CLSSviVal=@sviVal, CLSDf=@df,
        pH=@ph, Chldppm=@chl,
        ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
    WHERE CLSID=@id;
    SELECT @id;
END";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, m.ProcID, m.EquID, m.CompID);
        cmd.Parameters.AddWithValue("@age",    (decimal)m.ClsAge.Value);
        cmd.Parameters.AddWithValue("@ins",    m.ClsInsEff);
        cmd.Parameters.AddWithValue("@nins",   (decimal)nIns);
        cmd.Parameters.AddWithValue("@date",   m.InspectDate ?? DateTime.Today);
        cmd.Parameters.AddWithValue("@svi",    m.ClsSvi);
        cmd.Parameters.AddWithValue("@sviVal", (decimal)m.ClsSviVal);
        cmd.Parameters.AddWithValue("@df",     df);
        cmd.Parameters.AddWithValue("@ph",     (object?)m.PH ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@chl",    (object?)m.Chloride ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uid",    (object?)CurrentUserId() ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        if (id != null && id != DBNull.Value) m.ClsID = Convert.ToDecimal(id, CultureInfo.InvariantCulture);

        // TODO: legacy also called InspectionPlan(proc, equip, comp, "CLSCC", insEff, inspectDate) after save.
        //       Hook up the Blazor equivalent here, using THIS page's InsEff / InspectDate.
        return m;
    }

    public async Task<ClsccModel?> GetClsRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
SELECT TOP 1 CLSID, CLSAge, CLSInsEff, CLSnofIns, InspectDate, CLSSvi, CLSSviVal, CLSDf, pH, Chldppm
FROM dbo.CLSCC
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
ORDER BY CLSID DESC";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? S(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToString(o)?.Trim(); }
        decimal? D(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToDecimal(o, CultureInfo.InvariantCulture); }

        var m = new ClsccModel
        {
            ClsID = D("CLSID") ?? 0, ProcID = procId, EquID = equId, CompID = compId,
            ClsInsEff = S("CLSInsEff"),
            ClsNofIns = D("CLSnofIns") is { } n ? ((int)n).ToString(CultureInfo.InvariantCulture) : null,
            ClsSvi = S("CLSSvi"),
            ClsSviVal = D("CLSSviVal") is { } sv ? (int)sv : 0,
            ClsDf = D("CLSDf") is { } df ? df.ToString("0.###", CultureInfo.InvariantCulture) : null,
            PH = S("pH"), Chloride = S("Chldppm"),
            PHCode = CodeFor(PHOptions, S("pH")),
            ChlorideCode = CodeFor(ChlorideOptions, S("Chldppm")),
            KnownCrack = false   // not stored in dbo.CLSCC
        };
        if (D("CLSAge") is { } age) m.ClsAge = (int)age;
        if (r["InspectDate"] != DBNull.Value) m.InspectDate = Convert.ToDateTime(r["InspectDate"]);
        return m;
    }

    // Soft delete, using the table's own Deleted flag.
    public async Task DeleteClsAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
UPDATE dbo.CLSCC SET Deleted=1, ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
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
