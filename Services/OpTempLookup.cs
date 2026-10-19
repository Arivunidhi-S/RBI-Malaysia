using Microsoft.Data.SqlClient;
using System.Globalization;

namespace RBI_Malaysia.Services;

/// <summary>
/// Component-oda Operating Temperature (legacy: cboComponent.SelectedItem.Attributes["OPTemp"]).
/// Source: Tbl_EquipmentComponentDetails (CompAutoID, OPTemp - nvarchar(20)).
/// </summary>
public static class OpTempLookup
{
    public static async Task<double?> GetAsync(string cs, decimal compAutoId)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 OPTemp FROM Tbl_EquipmentComponentDetails WHERE CompAutoID=@c AND deleted=0", conn);
        cmd.Parameters.AddWithValue("@c", compAutoId);
        var v = await cmd.ExecuteScalarAsync();

        return v != null && v != DBNull.Value &&
               double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var t)
            ? t : null;
    }
}
