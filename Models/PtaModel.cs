using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models;

public class PtaModel
{
    // Keys
    public decimal PtaID  { get; set; }            // PTA.PTAID (0 = new record)
    public decimal ProcID { get; set; }
    public decimal EquID  { get; set; }
    public decimal CompID { get; set; }

    // Basic inputs
    [Required(ErrorMessage = "Age is required")]
    public int?      PtaAge      { get; set; }
    [Required(ErrorMessage = "Inspection effectiveness is required")]
    public string?   PtaInsEff   { get; set; }     // A-E (column name in Ref_SCC)
    [Required(ErrorMessage = "Number of inspections is required")]
    public string?   PtaNofIns   { get; set; }     // "1".."6"
    public DateTime? InspectDate { get; set; }

    // Susceptibility inputs
    public string? MaterialCode { get; set; }      // Material value in Ref_PTA
    public string? Material     { get; set; }      // label (stored in PTA.Material)
    public string? HeatCode     { get; set; }      // column name in Ref_PTA
    public string? Heat         { get; set; }      // label (stored in PTA.Heat)
    public string? PtaSvi       { get; set; }      // High / Medium / Low / None
    public int     PtaSviVal    { get; set; }      // 5000 / 500 / 50 / 1
    public bool    KnownCrack   { get; set; }      // not stored in dbo.PTA

    // Result
    public string? PtaDf { get; set; }
}
