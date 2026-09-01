namespace RBI_Malaysia.Models
{
    public class COFFlammableResult
    {
        public double W1 { get; set; }
        public double W2 { get; set; }
        public double W3 { get; set; }
        public double W4 { get; set; }

        public string Time1 { get; set; } = "";
        public string Time2 { get; set; } = "";
        public string Time3 { get; set; } = "";
        public string Time4 { get; set; } = "";

        public double T1 { get; set; }
        public double T2 { get; set; }
        public double T3 { get; set; }
        public double T4 { get; set; }

        public double Rate1 { get; set; }
        public double Rate2 { get; set; }
        public double Rate3 { get; set; }
        public double Rate4 { get; set; }

        public string ID1 { get; set; } = "";
        public string ID2 { get; set; } = "";
        public string ID3 { get; set; } = "";
        public string ID4 { get; set; } = "";

        public double Mass1 { get; set; }
        public double Mass2 { get; set; }
        public double Mass3 { get; set; }
        public double Mass4 { get; set; }

        public double Efficiency { get; set; }

        public double CAA { get; set; }
        public double CAB { get; set; }

        public double CAc1 { get; set; }
        public double CAc2 { get; set; }
        public double CAc3 { get; set; }
        public double CAc4 { get; set; }

        public double CAInsA { get; set; }
        public double CAInsB { get; set; }

        public double CAInst1 { get; set; }
        public double CAInst2 { get; set; }
        public double CAInst3 { get; set; }
        public double CAInst4 { get; set; }

        public double AInj { get; set; }
        public double BInj { get; set; }

        public double CAInj1 { get; set; }
        public double CAInj2 { get; set; }
        public double CAInj3 { get; set; }
        public double CAInj4 { get; set; }

        public double AInsInj { get; set; }
        public double BInsInj { get; set; }

        public double CAInsInj1 { get; set; }
        public double CAInsInj2 { get; set; }
        public double CAInsInj3 { get; set; }
        public double CAInsInj4 { get; set; }

        public double Factic1 { get; set; }
        public double Factic2 { get; set; }
        public double Factic3 { get; set; }
        public double Factic4 { get; set; }

        public double CAcmd1 { get; set; }
        public double CAcmd2 { get; set; }
        public double CAcmd3 { get; set; }
        public double CAcmd4 { get; set; }

        public double CAbleInj1 { get; set; }
        public double CAbleInj2 { get; set; }
        public double CAbleInj3 { get; set; }
        public double CAbleInj4 { get; set; }

        public double CAcmdFinal1 { get; set; }
        public double CAcmdFinal2 { get; set; }
        public double CAcmdFinal3 { get; set; }
        public double CAcmdFinal4 { get; set; }

        public double CAInjFinal1 { get; set; }
        public double CAInjFinal2 { get; set; }
        public double CAInjFinal3 { get; set; }
        public double CAInjFinal4 { get; set; }

        public double CAcmdTotal { get; set; }
        public double CAInjTotal { get; set; }

        public string CAcmdCategory { get; set; } = "";
        public string CAInjCategory { get; set; } = "";

        public double MaxValue { get; set; }
        public string MaxCategory { get; set; } = "";

        public double Ptrans { get; set; }
    }
}