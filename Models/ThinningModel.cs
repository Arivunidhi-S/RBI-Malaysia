using System;

namespace RBI_Malaysia.Models
{
    public class ThinningModel
    {
        public decimal TDFId { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public string? Clad { get; set; } // auto-filled from Tbl_EquipmentComponentDetails, read-only on screen
        public int? Age { get; set; }
        public string? InspectCate { get; set; } // Inspection Effectiveness category used for main TDF (A/B/C/D/E)
        public int? Nofins { get; set; } // Number of inspections for InspectCate
        public DateTime? InspectDate { get; set; }
        public string? ThinType { get; set; }
        public decimal? CompanyID { get; set; }

        // ---------------- Calculation Outputs (main) ----------------
        public decimal? Art { get; set; } // Thinning ratio, rounded to 2dp like old system
        public decimal? Tdf { get; set; } // Main Damage Factor (InspectCate + Nofins)

        // ---------------- New fields (added to table by user) ----------------
        public decimal? YS { get; set; } // Yield Strength
        public decimal? TS { get; set; } // Tensile Strength
        public decimal? S { get; set; }  // Allowable Stress
        public decimal? E { get; set; }  // Joint Efficiency

        public int? NofinsB { get; set; } // Number of inspections — Category B
        public int? NofinsC { get; set; } // Number of inspections — Category C
        public int? NofinsD { get; set; } // Number of inspections — Category D

        public decimal? Prp1_thin { get; set; } // plain manual entry (no formula yet)
        public decimal? Prp2_thin { get; set; }
        public decimal? Prp3_thin { get; set; }

        // DS1/DS2/DS3 are FIXED damage-scenario multipliers used in the Bayesian DF calculation
        // (1x, 2x, 4x the nominal corrosion rate) — always saved as 1, 2, 4. Not user-editable.
        public decimal? DS1 { get; set; }
        public decimal? DS2 { get; set; }
        public decimal? DS3 { get; set; }

        // Table 4.7 modification factors (manual input) — used in the final DF_thin multiplier
        public decimal? FIP { get; set; } // Inspection Program factor
        public decimal? FDL { get; set; } // Data Level / Design Life factor
        public decimal? FWD { get; set; } // Weld / Data factor
        public decimal? FAM { get; set; } // Additional Management factor
        public decimal? FSM { get; set; } // Safety Management factor
        public decimal? FOM { get; set; } // Other Management factor

        // Flow stress & Strength Ratio Parameter (persisted)
        public decimal? FSThin { get; set; }  // Flow stress
        public decimal? SRPThin { get; set; } // Strength Ratio Parameter

        // ---------------- Audit Fields ----------------
        public decimal? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public decimal? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
