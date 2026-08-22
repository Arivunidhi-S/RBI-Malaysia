using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Services;

public class EquipmentModel
{
    public decimal EquAutoID { get; set; }

    [Required(ErrorMessage = "Process Area is required.")]
    public decimal ProcessAreaID { get; set; }

    public string ProcessArea { get; set; } = "";

    [Required(ErrorMessage = "Equipment ID is required.")]
    public string EquPID { get; set; } = "";

    [Required(ErrorMessage = "Equipment Type is required.")]
    public string EqupType { get; set; } = "";

    public string EqupDescription { get; set; } = "";

    public string DoshNo { get; set; } = "";

    public string PID { get; set; } = "";

    public string IPLayer { get; set; } = "None";

    public string Instructive { get; set; } = "None";

    public string YearInstalled { get; set; } = "";

    public bool WTM { get; set; }

    public string HistoryDescription { get; set; } = "";

    public string InspectionTechniques { get; set; } = "";

    public string InspectionScope { get; set; } = "";

    public string RBIObservation { get; set; } = "";

    public string DOSHObservation { get; set; } = "";

    public string DesignCode { get; set; } = "";

    public decimal CompanyID { get; set; }

    public string CreatedBy { get; set; } = "";

    public string ModifiedBy { get; set; } = "";
}