using System;

namespace RBI_Malaysia.Models
{
    public class CausticModel
    {
        public decimal CauID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public int? CSAge { get; set; }           // Age
        public string? CSInsEff { get; set; }     // Inspection Effectiveness (A/B/C/D/E) — column name in Ref_SCC
        public int? CSnofIns { get; set; }        // No. of Inspections (1-6)
        public DateTime? InspectDate { get; set; }

        // Susceptibility: label stored in CSSvi, numeric value (1/50/500/5000) in CSSviVal
        public string? CSSvi { get; set; }        // "High" / "Medium" / "Low" / "None"
        public int? CSSviVal { get; set; }        // 5000 / 500 / 50 / 1

        // ---------------- Result ----------------
        public decimal? CSDf { get; set; }        // Df = Ref_SCC_lookup × Age^1.1

        // ---------------- Audit Fields ----------------
        public decimal? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public decimal? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
