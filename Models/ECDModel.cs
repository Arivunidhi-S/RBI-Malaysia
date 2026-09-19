using System;

namespace RBI_Malaysia.Models
{
    public class ECDModel
    {
        public decimal ECDID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- STEP 1 — furnished thickness & age ----------------
        public int? Agtk { get; set; }   // age (general, STEP1)
        // "t" (furnished thickness) is read live from Tbl_EquipmentComponentDetails.ReadVal — not stored separately.

        // ---------------- STEP 5 — t_rde / age_tke ----------------
        public decimal? Le { get; set; }        // measured wall loss due to external corrosion
        public decimal? TRde { get; set; }      // t_rde = t - Le
        public decimal? AgeTke { get; set; }    // time in service since t_rde was measured

        // ---------------- STEP 6/7/8/9 — coating adjustment ----------------
        public DateTime? CmpInstalDate { get; set; }  // Coating Installation Date -> cmpdt
        public DateTime? CalcDate { get; set; }       // Calculation Date -> caldt
        public string? CoatQualCode { get; set; }     // "1"=No/poor, "2"=Lower quality, "3"=High quality
        public decimal? AgeCoat { get; set; }         // age_coat = CalcDate - CmpInstalDate (STEP6) -> ECDagcoat
        public decimal? Cage { get; set; }            // expected coating life from CoatQualCode (STEP7): 0 / 5 / 15
        public bool CoatingFailed { get; set; }       // only relevant when age_tke < age_coat (STEP8 branch 1)
        public decimal? CoatAdj { get; set; }         // STEP8 result
        public decimal? Age { get; set; }             // final age = age_tke - CoatAdj (STEP9) -> ECDage

        // ---------------- STEP 4 — corrosion rate (manual — Driver/OpTemp lookup explicitly skipped) ----------------
        public decimal? Cr { get; set; }              // manual input, from inspection history / specialist assignment

        // ---------------- STEP 10 — material properties ----------------
        public decimal? YS { get; set; }
        public decimal? TS { get; set; }
        public decimal? S { get; set; }
        public decimal? E { get; set; } // weld joint efficiency

        // ---------------- STEP 11+ — results ----------------
        public decimal? ECDart { get; set; }   // Art = Cr × age / t_rde
        public decimal? FSExtcorr { get; set; }
        public decimal? SRPExtcorr { get; set; }
        public decimal? ECDDf { get; set; }    // final Damage Factor

        // Bayesian priors (same structure/constants as Thinning)
        public decimal? Prp1_ext { get; set; }
        public decimal? Prp2_ext { get; set; }
        public decimal? Prp3_ext { get; set; }
        public decimal? DS1 { get; set; } // fixed scenario constants 1/2/4 — informational
        public decimal? DS2 { get; set; }
        public decimal? DS3 { get; set; }

        // Table 4.7 modification factors
        public decimal? FIP { get; set; }
        public decimal? FDL { get; set; }
        public decimal? FWD { get; set; }
        public decimal? FAM { get; set; }
        public decimal? FSM { get; set; }
        public decimal? FOM { get; set; }

        // ---------------- Misc ----------------
        public string? InsEff { get; set; }        // Inspection Effectiveness — kept for record (not used in the Bayesian path)
        public int? Nofins { get; set; }
        public DateTime? InspectDate { get; set; }
        public decimal? CompanyID { get; set; }

        // ---------------- Audit Fields ----------------
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
