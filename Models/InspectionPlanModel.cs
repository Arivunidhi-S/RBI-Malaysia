using System;

namespace RBI_Malaysia.Models
{
    public class InspectionPlanModel
    {
        public decimal InspectID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // From InspectionPlan table
        public string? DamageFact { get; set; }    // Thinning / Caustic / Amine / Sulfide / H2S / Carbonate / PTA / CLSCC / HSC-HF / HIC/SOHIC-HF / CUI / ECD
        public string? InspectCate { get; set; }   // A / B / C / D / E
        public DateTime? InspectDate { get; set; }

        // Joined from InspectEffect table (read-only display)
        public string? InsEffCate { get; set; }    // e.g. "Highly Effective"
        public string? IntrInsp { get; set; }      // Intrusive Inspection recommendation
        public string? NonIntrInsp { get; set; }   // Non-Intrusive Inspection recommendation

        // Joined from master tables (display only)
        public string? ProcessArea { get; set; }
        public string? EquipmentName { get; set; }
        public string? ComponentName { get; set; }
    }
}
