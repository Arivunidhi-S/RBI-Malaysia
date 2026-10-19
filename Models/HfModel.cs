using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models;

public class HfModel
{
    // Keys
    public decimal HfID  { get; set; }            // HF.HFID (0 = new record)
    public decimal ProcID { get; set; }
    public decimal EquID  { get; set; }
    public decimal CompID { get; set; }

    // Basic inputs
    [Required(ErrorMessage = "Age is required")]
    public int?      HfAge      { get; set; }
    [Required(ErrorMessage = "Inspection effectiveness is required")]
    public string?   HfInsEff   { get; set; }     // A-E (column name in Ref_SCC)
    [Required(ErrorMessage = "Number of inspections is required")]
    public string?   HfNofIns   { get; set; }     // "1".."6"
    public DateTime? InspectDate { get; set; }

    // Susceptibility inputs
    public string? PwhtCode     { get; set; }      // "PWHT" / "Weld" (column name in Ref_HIC)
    public string? Pwht         { get; set; }      // label "Yes" / "No" (stored in HF.pwht)
    public string? MaterialCode { get; set; }      // stlsul value in Ref_HIC (1/2/3)
    public string? Material     { get; set; }      // label (stored in HF.Material)
    public string? HfSvi       { get; set; }      // High / Medium / Low / None
    public int     HfSviVal    { get; set; }      // 100 / 10 / 1
    public bool    KnownCrack   { get; set; }      // not stored in dbo.HF

    // Result
    public string? HfDf { get; set; }
}
