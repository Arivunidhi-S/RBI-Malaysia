using System;

namespace RBI_Malaysia.Models
{
    public class SulfidationModel
    {
        public decimal SulID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public int? SulAge { get; set; }
        public string? SulInsEff { get; set; }    // A / B / C / D / E
        public int? SulnofIns { get; set; }
        public DateTime? InspectDate { get; set; }

        // Step 1 — pH & H2S → Environmental Severity
        public string? PHwater { get; set; }      // stored as text label in pHwater column
        public string? H2S { get; set; }          // stored as text label in H2S column
        // PHwaterCode: DB column key used in Ref_Envi_Sev query (numeric e.g. 5.5, 7.5 etc.)
        public string? PHwaterCode { get; set; }
        // H2SCode: DB column name in Ref_Envi_Sev (Fiftyppm / Thosandppm / Tenppm / GtTenppm)
        public string? H2SCode { get; set; }

        // Step 2 — Env Severity + Heat + Brinnell → Svi text
        public string? Severity { get; set; }     // Environmental Severity (auto-calculated, stored in Severity col)
        public string? Heat { get; set; }         // As-Welded / PWHT — stored as text label in Heat column
        public string? HeatCode { get; set; }     // weld / pwht — used in Ref_EnviSul query
        public string? Brinnell { get; set; }     // stored as text label in Brinnell column
        public string? BrinnellCode { get; set; } // two / twothree / gttwothree — DB column name in Ref_EnviSul

        // Step 3 — Known crack checkbox (overrides Svi to "High")
        public bool KnownCrack { get; set; }

        // Svi — auto-computed from chain above OR overridden by KnownCrack
        public string? SulSvi { get; set; }       // "High" / "Medium" / "Low" / "None"
        public int? SulSviVal { get; set; }       // 100 / 10 / 1 / 1

        // ---------------- Result ----------------
        public decimal? SulDf { get; set; }

        // ---------------- Audit Fields ----------------
        public decimal? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public decimal? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
