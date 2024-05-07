using O2DESNet;
using Newtonsoft.Json;
using System;
using System.Text;

namespace SerializationTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var sim = new MySimModel();
            sim.Run(10);

            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            // Sim的序列化与反序列化
            string jsonString = JsonConvert.SerializeObject(sim, settings);
            // string jsonString = JsonConvert.SerializeObject(sim);
            Console.WriteLine("Sandbox JSON string >>>>>>>");
            Console.WriteLine(jsonString);
            var deserializedSimModel = JsonConvert.DeserializeObject<MySimModel>(jsonString);
            Console.WriteLine("deserialized SimModel >>>>>>> " + deserializedSimModel);
            Console.WriteLine(Sandbox2String(sim, deserializedSimModel));
            Console.WriteLine(HC2String(sim.HC_Queue, deserializedSimModel.HC_Queue));

            // HC的序列化与反序列化
            string jsonStr = JsonConvert.SerializeObject(sim.HC_Queue, settings);
            // string jsonStr = JsonConvert.SerializeObject(sim.HC_Queue);
            Console.WriteLine("Hour Counter JSON string >>>>>>>");
            Console.WriteLine(jsonStr);
            var deserializedHC = JsonConvert.DeserializeObject<HourCounter>(jsonStr);
            Console.WriteLine("deserialized HC >>>>>>> " + deserializedHC);
            Console.WriteLine(HC2String(sim.HC_Queue, deserializedHC));
        }

        static string Sandbox2String(Sandbox sb1, Sandbox sb2)
        {
            if (sb1 == null)
                return "SB1 is null";
            if (sb2 == null)
                return "SB2 is null";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "ClockTime", FDT(sb1.ClockTime), FDT(sb2.ClockTime)));
            Event e1 = sb1.GetHeadEvent();
            Event e2 = sb2.GetHeadEvent();
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "HeadEvent", e1.ToString(), e2.ToString()));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "HeadEventTime", FDT(sb1.HeadEventTime), FDT(sb2.HeadEventTime)));
            return sb.ToString();
        }

        static string HC2String(HourCounter? hc1, HourCounter? hc2)
        {
            if (hc1 == null)
                return "HC1 is null";
            if (hc2 == null)
                return "HC2 is null";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "LastCount", Math.Round(hc1.LastCount, 2), Math.Round(hc2.LastCount, 2)));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "TotalIncrement", Math.Round(hc1.TotalIncrement, 2), Math.Round(hc2.TotalIncrement, 2)));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "TotalDecrement", Math.Round(hc1.TotalDecrement, 2), Math.Round(hc2.TotalDecrement, 2)));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "TotalHours", Math.Round(hc1.TotalHours, 2), Math.Round(hc2.TotalHours, 2)));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "CumValue", Math.Round(hc1.CumValue, 2), Math.Round(hc2.CumValue, 2)));
            sb.AppendLine(string.Format("{0,20}: {1,10}, {2,10}", "AverageCount", Math.Round(hc1.AverageCount, 2), Math.Round(hc2.AverageCount, 2)));
            return sb.ToString();
        }

        private static string FDT(DateTime? dt)
        {
            if (dt == null)
                return string.Empty;
            else
                return ((DateTime)dt).ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}