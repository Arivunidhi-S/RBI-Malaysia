using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class HscService
{
    public record Option(string Code, string Label);

    private readonly string _cs;
    private readonly SulfidationService _lookups;   // reuse existing Process Area / Equipment / Component lookups
    private readonly UserSession _session;

    public HscService(IConfiguration config, SulfidationService lookups, UserSession session)
    {
        _cs = config.GetConnectionString("connString")
              ?? config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string missing.");
        _lookups = lookups;
        _session = session;
    }

    // Code = Heat value in Ref_HSC. Labels are exactly what the legacy page stored in HSC.pwht.
    public List<Option> PwhtOptions { get; } = new()
    {
        new("PWHT", "Yes"),
        new("Weld", "No"),
    };

    // Code = column name in Ref_HSC. Labels are exactly what the legacy page stored in HSC.Brinnel.
    public List<Option> BrinnelOptions { get; } = new()
    {
        new("two",        "< 200"),
        new("twothree",   "200 - 237"),
        new("gttwothree", "> 237"),
    };

    private static readonly string[] InsEffColumns = { "A", "B", "C", "D", "E" };

    // ---- Shared lookups (delegated) ----
    public Task<List<ProcessAreaModel>> GetProcessAreasAsync(object companyId) =>
        _lookups.GetProcessAreasAsync((dynamic)companyId);
    public Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId) =>
        _lookups.GetEquipmentsByProcessAsync(processAreaId);
    public Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId) =>
        _lookups.GetComponentsByEquipmentAsync(equipmentId);

    // ---- PWHT + Brinnell hardness -> Svi (legacy OnSelectedIndexChanged_HSC) ----
    public async Task<string> ComputeSviAsync(string pwhtCode, string brinnelCode)
    {
        if (!PwhtOptions.Exists(o => o.Code == pwhtCode)) throw new ArgumentException("Invalid PWHT value.");
        if (!BrinnelOptions.Exists(o => o.Code == brinnelCode)) throw new ArgumentException("Invalid Brinnell hardness.");

        // Column name comes from the whitelist above, never from raw input.
        var sql = $"SELECT [{brinnelCode}] FROM Ref_HSC WHERE Heat = @heat";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@heat", pwhtCode);
        var result = await cmd.ExecuteScalarAsync();
        var svi = result == null || result == DBNull.Value ? "" : Convert.ToString(result)!.Trim();
        if (svi.Length == 0) throw new InvalidOperationException("No susceptibility (Svi) found for the selected combination.");
        return svi;
    }

    // Same scale as HIC: High 100 / Medium 10 / Low 1 / None 1
    private static int SviToValue(string? svi) => svi?.Trim() switch
    {
        "High"   => 100,
        "Medium" => 10,
        "Low"    => 1,
        "None"   => 1,
        _ => throw new InvalidOperationException("Svi must be High, Medium, Low or None.")
    };

    // ---- Calculate (legacy btn_HSC_Calc_Click) : Df = base * Age^1.1 ----
    public async Task<HscModel> CalculateHscAsync(HscModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HscAge is null || string.IsNullOrEmpty(m.HscInsEff)
            || string.IsNullOrEmpty(m.HscNofIns) || string.IsNullOrEmpty(m.HscSvi))
            throw new InvalidOperationException("Please check all the fields.");

        if (!InsEffColumns.Contains(m.HscInsEff)) throw new InvalidOperationException("Invalid inspection effectiveness.");
        if (!int.TryParse(m.HscNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");

        m.HscSviVal = SviToValue(m.HscSvi);

        var sql = $"SELECT [{m.HscInsEff}] FROM Ref_SCC " +
                  "WHERE TRY_CAST(Inspection AS int) = @n AND TRY_CAST(Svi AS int) = @svi";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", nIns);
        cmd.Parameters.AddWithValue("@svi", m.HscSviVal);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value
            || !double.TryParse(Convert.ToString(result), NumberStyles.Float, CultureInfo.InvariantCulture, out var baseDf))
            throw new InvalidOperationException("No base damage factor found in Ref_SCC for the selected inputs.");

        var df = baseDf * Math.Pow(m.HscAge.Value, 1.1);
        m.HscDf = Math.Round(df, 3).ToString(CultureInfo.InvariantCulture);
        return m;
    }

    // ---- Persistence: dbo.HSC (what SulfideSave / sp_Carbanate_Save wrote for flags N5 / U5) ----
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

    public async Task<HscModel> SaveHscAsync(HscModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HscAge is null || string.IsNullOrEmpty(m.HscInsEff) || string.IsNullOrEmpty(m.HscNofIns)
            || string.IsNullOrEmpty(m.HscSvi) || string.IsNullOrEmpty(m.HscDf))
            throw new InvalidOperationException("Please check all the fields (and run Calculate first).");

        if (!int.TryParse(m.HscNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");
        if (!decimal.TryParse(m.HscDf, NumberStyles.Number, CultureInfo.InvariantCulture, out var df))
            throw new InvalidOperationException("Df value is invalid. Run Calculate again.");
        m.HscSviVal = SviToValue(m.HscSvi);

        m.Pwht    ??= Label(PwhtOptions, m.PwhtCode);
        m.Brinnel ??= Label(BrinnelOptions, m.BrinnelCode);

        // One active row per Process/Equipment/Component: update it if present (legacy "U5"), else insert ("N5").
        const string sql = @"
DECLARE @id numeric(18,0) = (SELECT TOP 1 HSCID FROM dbo.HSC
                             WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
                             ORDER BY HSCID DESC);
IF @id IS NULL
BEGIN
    INSERT INTO dbo.HSC
        (ProcID, EquID, CompID, HSCAge, HSCInsEff, HSCnofIns, InspectDate, HSCSvi, HSCSviVal, HSCDf,
         pwht, Brinnel, CreatedBy, CreatedDate, Rowversions, Deleted)
    VALUES
        (@p, @e, @c, @age, @ins, @nins, @date, @svi, @sviVal, @df,
         @pwht, @brin, @uid, GETDATE(), 0, 0);
    SELECT CAST(SCOPE_IDENTITY() AS numeric(18,0));
END
ELSE
BEGIN
    UPDATE dbo.HSC SET
        HSCAge=@age, HSCInsEff=@ins, HSCnofIns=@nins, InspectDate=@date, HSCSvi=@svi, HSCSviVal=@sviVal, HSCDf=@df,
        pwht=@pwht, Brinnel=@brin,
        ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
    WHERE HSCID=@id;
    SELECT @id;
END";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, m.ProcID, m.EquID, m.CompID);
        cmd.Parameters.AddWithValue("@age",    (decimal)m.HscAge.Value);
        cmd.Parameters.AddWithValue("@ins",    m.HscInsEff);
        cmd.Parameters.AddWithValue("@nins",   (decimal)nIns);
        cmd.Parameters.AddWithValue("@date",   m.InspectDate ?? DateTime.Today);
        cmd.Parameters.AddWithValue("@svi",    m.HscSvi);
        cmd.Parameters.AddWithValue("@sviVal", (decimal)m.HscSviVal);
        cmd.Parameters.AddWithValue("@df",     df);
        cmd.Parameters.AddWithValue("@pwht",   (object?)m.Pwht ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@brin",   (object?)m.Brinnel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uid",    (object?)CurrentUserId() ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        if (id != null && id != DBNull.Value) m.HscID = Convert.ToDecimal(id, CultureInfo.InvariantCulture);

        // TODO: legacy also called InspectionPlan(proc, equip, comp, "HSC-HF", insEff, inspectDate) after save.
        //       Hook up the Blazor equivalent here, using THIS page's InsEff / InspectDate.
        return m;
    }

    public async Task<HscModel?> GetHscRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
SELECT TOP 1 HSCID, HSCAge, HSCInsEff, HSCnofIns, InspectDate, HSCSvi, HSCSviVal, HSCDf, pwht, Brinnel
FROM dbo.HSC
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
ORDER BY HSCID DESC";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? S(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToString(o)?.Trim(); }
        decimal? D(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToDecimal(o, CultureInfo.InvariantCulture); }

        var m = new HscModel
        {
            HscID = D("HSCID") ?? 0, ProcID = procId, EquID = equId, CompID = compId,
            HscInsEff = S("HSCInsEff"),
            HscNofIns = D("HSCnofIns") is { } n ? ((int)n).ToString(CultureInfo.InvariantCulture) : null,
            HscSvi = S("HSCSvi"),
            HscSviVal = D("HSCSviVal") is { } sv ? (int)sv : 0,
            HscDf = D("HSCDf") is { } df ? df.ToString("0.###", CultureInfo.InvariantCulture) : null,
            Pwht = S("pwht"), Brinnel = S("Brinnel"),
            PwhtCode = CodeFor(PwhtOptions, S("pwht")),
            BrinnelCode = CodeFor(BrinnelOptions, S("Brinnel")),
            KnownCrack = false   // not stored in dbo.HSC
        };
        if (D("HSCAge") is { } age) m.HscAge = (int)age;
        if (r["InspectDate"] != DBNull.Value) m.InspectDate = Convert.ToDateTime(r["InspectDate"]);
        return m;
    }

    // Soft delete, using the table's own Deleted flag.
    public async Task DeleteHscAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
UPDATE dbo.HSC SET Deleted=1, ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
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
