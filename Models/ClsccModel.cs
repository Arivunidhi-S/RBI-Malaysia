using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models;

public class ClsccModel
{
    // Keys
    public decimal ClsID  { get; set; }            // CLSCC.CLSID (0 = new record)
    public decimal ProcID { get; set; }
    public decimal EquID  { get; set; }
    public decimal CompID { get; set; }

    // Basic inputs
    [Required(ErrorMessage = "Age is required")]
    public int?      ClsAge      { get; set; }
    [Required(ErrorMessage = "Inspection effectiveness is required")]
    public string?   ClsInsEff   { get; set; }     // A-E (column name in Ref_SCC)
    [Required(ErrorMessage = "Number of inspections is required")]
    public string?   ClsNofIns   { get; set; }     // "1".."6"
    public DateTime? InspectDate { get; set; }

    // Susceptibility inputs
    public string? PHCode       { get; set; }      // "LT" / "GT" (pH column value in Ref_CLSCC)
    public string? PH           { get; set; }      // label (stored in CLSCC.pH)
    public string? ChlorideCode { get; set; }      // column name in Ref_CLSCC
    public string? Chloride     { get; set; }      // label (stored in CLSCC.Chldppm)
    public string? ClsSvi       { get; set; }      // High / Medium / Low / None
    public int     ClsSviVal    { get; set; }      // 5000 / 500 / 50 / 1
    public bool    KnownCrack   { get; set; }      // not stored in dbo.CLSCC

    // Result
    public string? ClsDf { get; set; }
}
