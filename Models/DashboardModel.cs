namespace RBI_Malaysia.Models
{
    public class DashboardModel
    {
        public int TotalEquipment { get; set; }

        public int HighRisk { get; set; }

        public int MediumRisk { get; set; }

        public int LowRisk { get; set; }

        public int TotalInspections { get; set; }

        public int CompletedInspections { get; set; }

        public int PendingInspections { get; set; }

        public int InspectionStatus { get; set; }

        public int CriticalEquipment { get; set; }
    }
}