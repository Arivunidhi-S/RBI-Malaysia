using System;

namespace RBI_Malaysia.Models
{
    public class CUIModel
    {
        public decimal CUIID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public int? Agtk { get; set; }               // Age-tk (age since thickness reading)
        public string? InsEff { get; set; }           // Inspection Effectiveness code (A-E) — stored as-is
        public DateTime? CmpInstalDate { get; set; }   // Comp/Coating Install Date -> cmpdt
        public DateTime? CalcDate { get; set; }        // Calculation Date -> caldt
        public int? Nofins { get; set; }              // No. of Inspections
        public DateTime? InspectDate { get; set; }

        // Coating quality: code drives the age calc, label is what's persisted in `coatqual`
        public string? CoatQualCode { get; set; }      // "1"=Poor, "2"=Medium, "3"=High

        public decimal? CUIagcoat { get; set; }        // calculated: age since coating applied
        public decimal? CUIage { get; set; }           // calculated: min(Agtk, agecoat)

        public int? Fps { get; set; }                  // 1 or 2
        public int? Fip { get; set; }                  // 1 or 2

        // Cr driver: code selects the Ref_CUI column & drives the cr calc, label is what's persisted in `crdriver`
        public string? CrDriverCode { get; set; }       // marine / temp / arid / severe

        public decimal? Cr { get; set; }               // calculated corrosion rate
        public decimal? CUIart { get; set; }            // calculated Art
        public decimal? CUIDf { get; set; }             // calculated Damage Factor

        // Fins/Fcm/Fic: bound by their numeric VALUE (used directly in the cr formula);
        // the matching label text is what actually gets persisted in fins/fcm/fic (old-system behaviour)
        public decimal? Fins { get; set; }
        public decimal? Fcm { get; set; }
        public decimal? Fic { get; set; }

        public decimal? CompanyID { get; set; }

        // ---------------- Audit Fields ----------------
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
