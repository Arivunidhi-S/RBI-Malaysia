using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class HfService
{
    public record Option(string Code, string Label);

    private readonly string _cs;
    private readonly SulfidationService _lookups;   // reuse existing Process Area / Equipment / Component lookups
    private readonly UserSession _session;

    public HfService(IConfiguration config, SulfidationService lookups, UserSession session)
    {
        _cs = config.GetConnectionString("connString")
              ?? config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string missing.");
        _lookups = lookups;
        _session = session;
    }

    // Code = column name in Ref_HIC. Labels are exactly what the legacy page stored in HF.pwht.
    public List<Option> PwhtOptions { get; } = new()
    {
        new("PWHT", "Yes"),
        new("Weld", "No"),
    };

    // Code = stlsul value in Ref_HIC. Labels are exactly what the legacy page stored in HF.Material.
    public List<Option> MaterialOptions { get; } = new()
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

    // ---- PWHT + Construction material -> Svi (legacy OnSelectedIndexChanged_HF) ----
    // Environmental severity is fixed at "High" for HF service (legacy: Envi='High').
    public async Task<string> ComputeSviAsync(string pwhtCode, string materialCode)
    {
        if (!PwhtOptions.Exists(o => o.Code == pwhtCode)) throw new ArgumentException("Invalid PWHT value.");
        if (!MaterialOptions.Exists(o => o.Code == materialCode)) throw new ArgumentException("Invalid construction material.");

        // Column name comes from the whitelist above, never from raw input.
        var sql = $"SELECT [{pwhtCode}] FROM Ref_HIC WHERE stlsul = @stl AND Envi = 'High'";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@stl", materialCode);
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

    // ---- Calculate (legacy btn_HF_Calc_Click) : Df = base * Age^1.1 ----
    public async Task<HfModel> CalculateHfAsync(HfModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HfAge is null || string.IsNullOrEmpty(m.HfInsEff)
            || string.IsNullOrEmpty(m.HfNofIns) || string.IsNullOrEmpty(m.HfSvi))
            throw new InvalidOperationException("Please check all the fields.");

        if (!InsEffColumns.Contains(m.HfInsEff)) throw new InvalidOperationException("Invalid inspection effectiveness.");
        if (!int.TryParse(m.HfNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");

        m.HfSviVal = SviToValue(m.HfSvi);

        var sql = $"SELECT [{m.HfInsEff}] FROM Ref_SCC " +
                  "WHERE TRY_CAST(Inspection AS int) = @n AND TRY_CAST(Svi AS int) = @svi";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@n", nIns);
        cmd.Parameters.AddWithValue("@svi", m.HfSviVal);
        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value
            || !double.TryParse(Convert.ToString(result), NumberStyles.Float, CultureInfo.InvariantCulture, out var baseDf))
            throw new InvalidOperationException("No base damage factor found in Ref_SCC for the selected inputs.");

        var df = baseDf * Math.Pow(m.HfAge.Value, 1.1);
        m.HfDf = Math.Round(df, 3).ToString(CultureInfo.InvariantCulture);
        return m;
    }

    // ---- Persistence: dbo.HF (what SulfideSave / sp_Carbanate_Save wrote for flags N6 / U6) ----
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

    public async Task<HfModel> SaveHfAsync(HfModel m)
    {
        if (m.ProcID <= 0 || m.EquID <= 0 || m.CompID <= 0
            || m.HfAge is null || string.IsNullOrEmpty(m.HfInsEff) || string.IsNullOrEmpty(m.HfNofIns)
            || string.IsNullOrEmpty(m.HfSvi) || string.IsNullOrEmpty(m.HfDf))
            throw new InvalidOperationException("Please check all the fields (and run Calculate first).");

        if (!int.TryParse(m.HfNofIns, out var nIns)) throw new InvalidOperationException("Invalid number of inspections.");
        if (!decimal.TryParse(m.HfDf, NumberStyles.Number, CultureInfo.InvariantCulture, out var df))
            throw new InvalidOperationException("Df value is invalid. Run Calculate again.");
        m.HfSviVal = SviToValue(m.HfSvi);

        m.Pwht    ??= Label(PwhtOptions, m.PwhtCode);
        m.Material ??= Label(MaterialOptions, m.MaterialCode);

        // One active row per Process/Equipment/Component: update it if present (legacy "U6"), else insert ("N6").
        const string sql = @"
DECLARE @id numeric(18,0) = (SELECT TOP 1 HFID FROM dbo.HF
                             WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
                             ORDER BY HFID DESC);
IF @id IS NULL
BEGIN
    INSERT INTO dbo.HF
        (ProcID, EquID, CompID, HFAge, HFInsEff, HFnofIns, InspectDate, HFSvi, HFSviVal, HFDf,
         pwht, Material, CreatedBy, CreatedDate, Rowversions, Deleted)
    VALUES
        (@p, @e, @c, @age, @ins, @nins, @date, @svi, @sviVal, @df,
         @pwht, @mat, @uid, GETDATE(), 0, 0);
    SELECT CAST(SCOPE_IDENTITY() AS numeric(18,0));
END
ELSE
BEGIN
    UPDATE dbo.HF SET
        HFAge=@age, HFInsEff=@ins, HFnofIns=@nins, InspectDate=@date, HFSvi=@svi, HFSviVal=@sviVal, HFDf=@df,
        pwht=@pwht, Material=@mat,
        ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
    WHERE HFID=@id;
    SELECT @id;
END";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, m.ProcID, m.EquID, m.CompID);
        cmd.Parameters.AddWithValue("@age",    (decimal)m.HfAge.Value);
        cmd.Parameters.AddWithValue("@ins",    m.HfInsEff);
        cmd.Parameters.AddWithValue("@nins",   (decimal)nIns);
        cmd.Parameters.AddWithValue("@date",   m.InspectDate ?? DateTime.Today);
        cmd.Parameters.AddWithValue("@svi",    m.HfSvi);
        cmd.Parameters.AddWithValue("@sviVal", (decimal)m.HfSviVal);
        cmd.Parameters.AddWithValue("@df",     df);
        cmd.Parameters.AddWithValue("@pwht",   (object?)m.Pwht ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mat",   (object?)m.Material ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uid",    (object?)CurrentUserId() ?? DBNull.Value);

        var id = await cmd.ExecuteScalarAsync();
        if (id != null && id != DBNull.Value) m.HfID = Convert.ToDecimal(id, CultureInfo.InvariantCulture);

        // TODO: legacy also called InspectionPlan(proc, equip, comp, "HIC/SOHIC-HF", insEff, inspectDate) after save.
        //       Hook up the Blazor equivalent here, using THIS page's InsEff / InspectDate.
        return m;
    }

    public async Task<HfModel?> GetHfRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
SELECT TOP 1 HFID, HFAge, HFInsEff, HFnofIns, InspectDate, HFSvi, HFSviVal, HFDf, pwht, Material
FROM dbo.HF
WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0
ORDER BY HFID DESC";

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        AddKeys(cmd, procId, equId, compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        string? S(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToString(o)?.Trim(); }
        decimal? D(string col) { var o = r[col]; return o == DBNull.Value ? null : Convert.ToDecimal(o, CultureInfo.InvariantCulture); }

        var m = new HfModel
        {
            HfID = D("HFID") ?? 0, ProcID = procId, EquID = equId, CompID = compId,
            HfInsEff = S("HFInsEff"),
            HfNofIns = D("HFnofIns") is { } n ? ((int)n).ToString(CultureInfo.InvariantCulture) : null,
            HfSvi = S("HFSvi"),
            HfSviVal = D("HFSviVal") is { } sv ? (int)sv : 0,
            HfDf = D("HFDf") is { } df ? df.ToString("0.###", CultureInfo.InvariantCulture) : null,
            Pwht = S("pwht"), Material = S("Material"),
            PwhtCode = CodeFor(PwhtOptions, S("pwht")),
            MaterialCode = CodeFor(MaterialOptions, S("Material")),
            KnownCrack = false   // not stored in dbo.HF
        };
        if (D("HFAge") is { } age) m.HfAge = (int)age;
        if (r["InspectDate"] != DBNull.Value) m.InspectDate = Convert.ToDateTime(r["InspectDate"]);
        return m;
    }

    // Soft delete, using the table's own Deleted flag.
    public async Task DeleteHfAsync(decimal procId, decimal equId, decimal compId)
    {
        const string sql = @"
UPDATE dbo.HF SET Deleted=1, ModifiedBy=@uid, ModifiedDate=GETDATE(), Rowversions=ISNULL(Rowversions,0)+1
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
