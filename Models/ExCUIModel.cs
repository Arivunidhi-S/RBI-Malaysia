namespace RBI_Malaysia.Models;

public class ExCUIModel
{
    public decimal ExCUIID { get; set; }
    public decimal ProcID { get; set; }
    public decimal EquID { get; set; }
    public decimal CompID { get; set; }
    public decimal? CompanyID { get; set; }

    public DateTime? InspectDate { get; set; }
    public string? InsEff { get; set; }            // A-E
    public DateTime? CmpInstalDate { get; set; }
    public DateTime? CalcDate { get; set; }
    public int? Nofins { get; set; }
    public decimal? Agtk { get; set; }

    public string? CoatQualCode { get; set; }      // 1/2/3
    public string? CrDriverCode { get; set; }      // marine/temp/arid/severe
    public string? PipCode { get; set; }           // Piping Complexity 1/2/3
    public string? InsConCode { get; set; }        // Insulation Condition 1/2/3
    public string? ChlrFreeCode { get; set; }      // 2 = Contain Chloride, 1 = Chloride Free

    public decimal? Age { get; set; }
    public string? Svi { get; set; }
    public decimal? Df { get; set; }               // ExCUIDf
}
