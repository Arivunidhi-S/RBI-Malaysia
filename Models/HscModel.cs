using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models;

public class HscModel
{
    // Keys
    public decimal HscID  { get; set; }            // HSC.HSCID (0 = new record)
    public decimal ProcID { get; set; }
    public decimal EquID  { get; set; }
    public decimal CompID { get; set; }

    // Basic inputs
    [Required(ErrorMessage = "Age is required")]
    public int?      HscAge      { get; set; }
    [Required(ErrorMessage = "Inspection effectiveness is required")]
    public string?   HscInsEff   { get; set; }     // A-E (column name in Ref_SCC)
    [Required(ErrorMessage = "Number of inspections is required")]
    public string?   HscNofIns   { get; set; }     // "1".."6"
    public DateTime? InspectDate { get; set; }

    // Susceptibility inputs
    public string? PwhtCode     { get; set; }      // "PWHT" / "Weld" (Heat value in Ref_HSC)
    public string? Pwht         { get; set; }      // label "Yes" / "No" (stored in HSC.pwht)
    public string? BrinnelCode  { get; set; }      // column name in Ref_HSC
    public string? Brinnel      { get; set; }      // label (stored in HSC.Brinnel)
    public string? HscSvi       { get; set; }      // High / Medium / Low / None
    public int     HscSviVal    { get; set; }      // 100 / 10 / 1
    public bool    KnownCrack   { get; set; }      // not stored in dbo.HSC

    // Result
    public string? HscDf { get; set; }
}
