using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models;

public class HicSohicModel
{
    // Keys
    public decimal HicID  { get; set; }            // HIC.HICID (0 = new record)
    public decimal ProcID { get; set; }
    public decimal EquID  { get; set; }
    public decimal CompID { get; set; }

    // Basic inputs
    [Required(ErrorMessage = "Age is required")]
    public int?      HicAge     { get; set; }
    [Required(ErrorMessage = "Inspection effectiveness is required")]
    public string?   HicInsEff  { get; set; }      // A-E (column name in Ref_SCC)
    [Required(ErrorMessage = "Number of inspections is required")]
    public string?   HicnofIns  { get; set; }      // "1".."6"
    public DateTime? InspectDate { get; set; }

    // Step 1: environmental severity
    public string? PHwaterCode { get; set; }       // pHofWater value in Ref_Envi_Sev
    public string? PHwater     { get; set; }       // label
    public string? H2SCode     { get; set; }       // column name in Ref_Envi_Sev
    public string? H2S         { get; set; }       // label
    public string? Severity    { get; set; }

    // Step 2: susceptibility
    public string? HeatCode        { get; set; }   // column name in Ref_HIC (weld / pwht)
    public string? Heat            { get; set; }
    public string? SteelSulfurCode { get; set; }   // stlsul in Ref_HIC (1/2/3)
    public string? SteelSulfur     { get; set; }
    public string? HicSvi          { get; set; }   // High / Medium / Low / None
    public int     HicSviVal       { get; set; }   // 100 / 10 / 1
    public bool    KnownCrack      { get; set; }

    // Result
    public string? HicDfSOHIC { get; set; }
}
