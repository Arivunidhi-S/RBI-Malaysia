using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class ExCUIService
{
    private readonly string _cs;
    public ExCUIService(IConfiguration config)
        => _cs = config.GetConnectionString("connString") ?? throw new InvalidOperationException("Connection string missing");

    private static decimal? Dec(object? v)
        => v != null && v != DBNull.Value &&
           decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    private static DateTime? Dt(object? v) => v is DateTime d ? d : null;
    private static string? Str(object? v) => v == null || v == DBNull.Value ? null : v.ToString()?.Trim();

    // Age-tk : ExCLS-la ullathe logic (Agecal equivalent)
    public async Task<decimal?> GetAgeTkAsync(int inspectYear, decimal procId, decimal equId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 yearinstalled FROM Tbl_EquipmentAsset WHERE deleted=0 AND ProcessareaID=@p AND EquAutoID=@e", conn);
        cmd.Parameters.AddWithValue("@p", procId);
        cmd.Parameters.AddWithValue("@e", equId);
        var yi = Dec(await cmd.ExecuteScalarAsync());
        return yi == null ? null : Math.Max(0, inspectYear - yi.Value);
    }

    // Coating age : ExCLS-oda same (legacy-la CUI handler paarkala - same pattern nu assume)
    public static void ComputeCoatAge(ExCUIModel m)
    {
        if (m.CmpInstalDate is null || m.CalcDate is null || string.IsNullOrEmpty(m.CoatQualCode)) { m.Age = null; return; }
        DateTime? start = m.CoatQualCode switch
        {
            "1" => m.CmpInstalDate.Value,
            "2" => m.CmpInstalDate.Value.AddYears(5),
            "3" => m.CmpInstalDate.Value.AddYears(15),
            _ => null
        };
        if (start is null) { m.Age = null; return; }
        m.Age = Math.Max(0, (int)(m.CalcDate.Value - start.Value).TotalDays / 365);
    }

    // ---------- Svi (legacy sviload()) ----------
    public async Task<string?> GetSviAsync(decimal compId, string areaCode)
    {
        if (!ExCLSLookups.Areas.Any(a => a.Code == areaCode)) return null;   // column whitelist

        double opTemp = await OpTempLookup.GetAsync(_cs, compId)
                        ?? throw new InvalidOperationException("Operating Temperature not found for this component");

        double bucket = opTemp < 49 ? 49 : opTemp < 93 ? 93 : opTemp < 149 ? 149 : 150;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT [{areaCode}] FROM Ref_ExCUI WHERE opTemp=@t", conn);
        cmd.Parameters.AddWithValue("@t", bucket.ToString(CultureInfo.InvariantCulture));
        return Str(await cmd.ExecuteScalarAsync());
    }

    // ---------- Df (legacy btn_ExCUI_Calc_Click) ----------
    public async Task<ExCUIModel> CalculateAsync(ExCUIModel m)
    {
        int sviVal = m.Svi switch { "High" => 50, "Medium" => 10, "Low" => 1, "None" => 1, _ => 0 };
        if (sviVal == 0) throw new InvalidOperationException("Svi value not available. Select Area.");
        if (m.InsEff is not ("A" or "B" or "C" or "D" or "E")) throw new InvalidOperationException("Invalid Inspection Effectiveness.");
        if (m.Nofins is null) throw new InvalidOperationException("Select No of Inspection.");
        if (m.Age is null) throw new InvalidOperationException("Age not calculated. Check Coating Quality & dates.");

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT [{m.InsEff}] FROM Ref_SCC WHERE Inspection=@i AND Svi=@s", conn);
        cmd.Parameters.AddWithValue("@i", m.Nofins.Value);
        cmd.Parameters.AddWithValue("@s", sviVal);

        var baseDf = Dec(await cmd.ExecuteScalarAsync())
                     ?? throw new InvalidOperationException("Ref_SCC la matching row illa.");

        // Df = df * age^1.1
        var dfb = (double)baseDf * Math.Pow((double)m.Age.Value, 1.1);
        if (double.IsNaN(dfb) || double.IsInfinity(dfb)) dfb = 0;
        m.Df = Math.Round((decimal)dfb, 3);
        return m;
    }

    public async Task<ExCUIModel?> GetRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT TOP 1 * FROM ExCUI
            WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0 ORDER BY ExCUIID DESC", conn);
        cmd.Parameters.AddWithValue("@p", procId);
        cmd.Parameters.AddWithValue("@e", equId);
        cmd.Parameters.AddWithValue("@c", compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new ExCUIModel
        {
            ExCUIID = Dec(r["ExCUIID"]) ?? 0,
            ProcID = Dec(r["ProcID"]) ?? 0,
            EquID = Dec(r["EquID"]) ?? 0,
            CompID = Dec(r["CompID"]) ?? 0,
            Agtk = Dec(r["Agtk"]),
            InsEff = Str(r["InsEff"]),
            CmpInstalDate = Dt(r["cmpdt"]),
            CalcDate = Dt(r["caldt"]),
            Nofins = (int?)Dec(r["nofins"]),
            InspectDate = Dt(r["InspectDate"]),
            CoatQualCode = ExCLSLookups.CoatCode(Str(r["coatqual"])),
            Age = Dec(r["age"]),
            CrDriverCode = ExCLSLookups.AreaCode(Str(r["crdriver"])),
            PipCode = ExCLSLookups.CodeOf(ExCLSLookups.Pip, Str(r["pip"])),
            InsConCode = ExCLSLookups.CodeOf(ExCLSLookups.InsCon, Str(r["inscon"])),
            ChlrFreeCode = ExCLSLookups.CodeOf(ExCLSLookups.Chloride, Str(r["chlrfree"])),
            Svi = Str(r["Svi"]),
            Df = Dec(r["ExCUIDf"]),
            CompanyID = Dec(r["CompanyID"])
        };
    }

    public async Task<ExCUIModel> SaveAsync(ExCUIModel m, string userId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("sp_ExCUI_Save", conn) { CommandType = CommandType.StoredProcedure };

        bool isNew = m.ExCUIID <= 0;
        cmd.Parameters.AddWithValue("@idp", isNew ? 0 : m.ExCUIID);
        cmd.Parameters.AddWithValue("@ProcessareaIDp", m.ProcID);
        cmd.Parameters.AddWithValue("@EqupIDp", m.EquID);
        cmd.Parameters.AddWithValue("@CompIDp", m.CompID);
        cmd.Parameters.AddWithValue("@agetkp", m.Agtk ?? 0);
        cmd.Parameters.AddWithValue("@InsEffp", m.InsEff ?? "");
        cmd.Parameters.AddWithValue("@compDtp", (object?)m.CmpInstalDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@calDtp", (object?)m.CalcDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@NofInsp", m.Nofins ?? 0);
        cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@coatqualp", ExCLSLookups.CoatText(m.CoatQualCode));
        cmd.Parameters.AddWithValue("@agep", m.Age ?? 0);
        cmd.Parameters.AddWithValue("@CrDriverp", ExCLSLookups.AreaText(m.CrDriverCode));
        cmd.Parameters.AddWithValue("@pipp", ExCLSLookups.TextOf(ExCLSLookups.Pip, m.PipCode));
        cmd.Parameters.AddWithValue("@insconp", ExCLSLookups.TextOf(ExCLSLookups.InsCon, m.InsConCode));
        cmd.Parameters.AddWithValue("@chlrfreep", ExCLSLookups.TextOf(ExCLSLookups.Chloride, m.ChlrFreeCode));
        cmd.Parameters.AddWithValue("@svip", m.Svi ?? "");
        cmd.Parameters.AddWithValue("@Dfexclsp", m.Df ?? 0);
        cmd.Parameters.AddWithValue("@CompanyID", m.CompanyID ?? 0);
        cmd.Parameters.AddWithValue("@useridp", userId);
        cmd.Parameters.AddWithValue("@saveflagp", isNew ? "N" : "U");
        await cmd.ExecuteNonQueryAsync();

        return await GetRecordAsync(m.ProcID, m.EquID, m.CompID) ?? m;
    }

    public async Task DeleteAsync(decimal id, string userId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "UPDATE ExCUI SET Deleted=1, ModifiedBy=@u, ModifiedDate=GETDATE() WHERE ExCUIID=@id", conn);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
