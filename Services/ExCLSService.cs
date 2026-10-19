using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services;

public class ExCLSService
{
    private readonly string _cs;

    // ECDService/ThinningService la use pannra connection pattern-ku match pannunga
    public ExCLSService(IConfiguration config)
        => _cs = config.GetConnectionString("connString") ?? throw new InvalidOperationException("Connection string missing");

    // ---------- helpers ----------
    private static decimal? Dec(object? v)
        => v != null && v != DBNull.Value &&
           decimal.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateTime? Dt(object? v) => v is DateTime d ? d : null;
    private static string? Str(object? v) => v == null || v == DBNull.Value ? null : v.ToString()?.Trim();

    // ---------- Age-tk (inspection year - year installed) ----------
    // NOTE: Legacy Agecal() equivalent. Unga existing Agecal logic vera irundha, inga replace pannunga.
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

    // ---------- Coating age (legacy OnSelectedIndexChanged_ExCLS_CoatQual) ----------
    public static void ComputeCoatAge(ExCLSModel m)
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

        int years = (int)(m.CalcDate.Value - start.Value).TotalDays / 365;   // integer years - legacy maari
        m.Age = Math.Max(0, years);
    }

    // ---------- Svi (legacy OnSelectedIndexChanged_ExCLS_crb) ----------
    // OPTemp-ah service-ae DB-la irundhu edukkum (ComponentModel.OPTemp thevai illa)
    public async Task<string?> GetSviAsync(decimal compId, string areaCode)
    {
        // column name dynamic -> whitelist (SQL injection thavirkka)
        if (!ExCLSLookups.Areas.Any(a => a.Code == areaCode)) return null;

        double opTemp = await OpTempLookup.GetAsync(_cs, compId)
                        ?? throw new InvalidOperationException("Operating Temperature not found for this component");

        double bucket = opTemp < 49 ? 49 : opTemp < 93 ? 93 : opTemp < 149 ? 149 : 150;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand($"SELECT [{areaCode}] FROM Ref_ExCLSCC WHERE opTemp=@t", conn);
        cmd.Parameters.AddWithValue("@t", bucket.ToString(CultureInfo.InvariantCulture));
        return Str(await cmd.ExecuteScalarAsync());
    }

    // ---------- Df (legacy btn_ExCLS_Calc_Click) ----------
    public async Task<ExCLSModel> CalculateAsync(ExCLSModel m)
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

    // ---------- Get ----------
    public async Task<ExCLSModel?> GetRecordAsync(decimal procId, decimal equId, decimal compId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT TOP 1 * FROM ExCLS
            WHERE ProcID=@p AND EquID=@e AND CompID=@c AND ISNULL(Deleted,0)=0 ORDER BY ExCLSID DESC", conn);
        cmd.Parameters.AddWithValue("@p", procId);
        cmd.Parameters.AddWithValue("@e", equId);
        cmd.Parameters.AddWithValue("@c", compId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        var coatText = Str(r["coatqual"]);
        var areaText = Str(r["crdriver"]);
        return new ExCLSModel
        {
            ExCLSID = Dec(r["ExCLSID"]) ?? 0,
            ProcID = Dec(r["ProcID"]) ?? 0,
            EquID = Dec(r["EquID"]) ?? 0,
            CompID = Dec(r["CompID"]) ?? 0,
            Agtk = Dec(r["Agtk"]),
            InsEff = Str(r["InsEff"]),
            CmpInstalDate = Dt(r["cmpdt"]),
            CalcDate = Dt(r["caldt"]),
            Nofins = (int?)Dec(r["nofins"]),
            InspectDate = Dt(r["InspectDate"]),
            CoatQualCode = ExCLSLookups.CoatCode(coatText),
            Age = Dec(r["age"]),
            CrDriverCode = ExCLSLookups.AreaCode(areaText),
            Svi = Str(r["Svi"]),
            Df = Dec(r["ExCLSDf"]),
            CompanyID = Dec(r["CompanyID"])
        };
    }

    // ---------- Save (Insert / Update via sp_ExCLS_Save) ----------
    public async Task<ExCLSModel> SaveAsync(ExCLSModel m, string userId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("sp_ExCLS_Save", conn) { CommandType = CommandType.StoredProcedure };

        bool isNew = m.ExCLSID <= 0;
        cmd.Parameters.AddWithValue("@idp", isNew ? 0 : m.ExCLSID);
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
        cmd.Parameters.AddWithValue("@svip", m.Svi ?? "");
        cmd.Parameters.AddWithValue("@Dfexclsp", m.Df ?? 0);
        cmd.Parameters.AddWithValue("@CompanyID", m.CompanyID ?? 0);   // CompanyID UserSession-la irundhu set pannanum
        cmd.Parameters.AddWithValue("@useridp", userId);
        cmd.Parameters.AddWithValue("@saveflagp", isNew ? "N" : "U");
        await cmd.ExecuteNonQueryAsync();

        return await GetRecordAsync(m.ProcID, m.EquID, m.CompID) ?? m;
    }

    // ---------- Delete (soft delete) ----------
    public async Task DeleteAsync(decimal exclsId, string userId)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "UPDATE ExCLS SET Deleted=1, ModifiedBy=@u, ModifiedDate=GETDATE() WHERE ExCLSID=@id", conn);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@id", exclsId);
        await cmd.ExecuteNonQueryAsync();
    }
}
