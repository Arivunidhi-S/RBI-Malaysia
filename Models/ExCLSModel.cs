namespace RBI_Malaysia.Models;

public class ExCLSModel
{
    public decimal ExCLSID { get; set; }
    public decimal ProcID { get; set; }
    public decimal EquID { get; set; }
    public decimal CompID { get; set; }
    public decimal? CompanyID { get; set; }

    // Inputs
    public DateTime? InspectDate { get; set; }
    public string? InsEff { get; set; }            // A-E
    public DateTime? CmpInstalDate { get; set; }   // cmpdt
    public DateTime? CalcDate { get; set; }        // caldt
    public int? Nofins { get; set; }               // 1-6
    public decimal? Agtk { get; set; }             // Age-tk
    public string? CoatQualCode { get; set; }      // 1/2/3 (UI only - DB stores the text)
    public string? CrDriverCode { get; set; }      // marine/temp/arid/severe (UI only - DB stores the text)

    // Calculated
    public decimal? Age { get; set; }
    public string? Svi { get; set; }               // High/Medium/Low/None
    public decimal? Df { get; set; }               // ExCLSDf
}

public static class ExCLSLookups
{
    // Legacy-la DB-la Text thaan save aagum - athe maintain pannrom (existing data compatible)
    public static readonly (string Code, string Text)[] CoatQualities =
    {
        ("1", "No Coating or Poor Quality"),
        ("2", "Medium Quality"),
        ("3", "High Quality"),
    };

    public static readonly (string Code, string Text)[] Areas =
    {
        ("marine", "Marine/Cooling Tower Drift Area"),
        ("temp",   "Temperature"),
        ("arid",   "Arid/Dry"),
        ("severe", "Severe"),
    };

    public static readonly (string Code, string Text)[] InspectionEffectiveness =
    {
        ("A", "Highly Effective"),
        ("B", "Usually Effective"),
        ("C", "Fairly Effective"),
        ("D", "Poorly Effective"),
        ("E", "In Effective"),
    };

    // DB column nvarchar(20) -> 20 char-ku mela truncate aagum ("No Coating or Poor Quality" = 26 chars).
    // So text-> code mapping truncated value-vum match aagura maari.
    private static bool Same(string full, string? stored)
        => !string.IsNullOrEmpty(stored) && (full == stored || (full.Length > 20 && full[..20] == stored));

    public static readonly (string Code, string Text)[] Pip =
    {
        ("1", "Below Average"), ("2", "Average"), ("3", "Above Average"),
    };
    public static readonly (string Code, string Text)[] InsCon =
    {
        ("1", "Below Average"), ("2", "Average"), ("3", "Above Average"),
    };
    public static readonly (string Code, string Text)[] Chloride =
    {
        ("2", "Contain Chloride"), ("1", "Chloride Free"),
    };

    public static string TextOf((string Code, string Text)[] list, string? code) => list.FirstOrDefault(x => x.Code == code).Text ?? "";
    public static string CodeOf((string Code, string Text)[] list, string? stored) => list.FirstOrDefault(x => Same(x.Text, stored)).Code ?? "";

    public static string CoatText(string? code) => TextOf(CoatQualities, code);
    public static string CoatCode(string? text) => CodeOf(CoatQualities, text);
    public static string AreaText(string? code) => TextOf(Areas, code);
    public static string AreaCode(string? text) => CodeOf(Areas, text);
}
