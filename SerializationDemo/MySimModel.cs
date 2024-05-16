using Newtonsoft.Json;
using O2DESNet;

namespace SerializationTest
{
    class MySimModel : Sandbox
    {
        [JsonProperty("Q")]
        private int Q = 0;
        [JsonProperty("S")]
        private int S = 0;
        public HourCounter HC_Queue;
        public HourCounter HC_Server;
        public int Dummy { get; private set; }

        void Arrive()
        {
            Console.WriteLine($"{ClockTime}\t Arrived.");
            // Schedule(Arrive, Exponential.Sample(DefaultRS, TimeSpan.FromHours(1.0 / 4)));
            Schedule(Arrive, TimeSpan.FromHours(1.0 / 4));

            Q++;
            HC_Queue.ObserveChange(1);
            if (S == 0)
                Schedule(Start);
        }

        void Start()
        {
            Console.WriteLine($"{ClockTime}\t Started.");
            // Schedule(Depart, Exponential.Sample(DefaultRS, TimeSpan.FromHours(1.0 / 5)));
            Schedule(Depart, TimeSpan.FromHours(1.0 / 5));

            Q--;
            S++;
            HC_Queue.ObserveChange(-1);
            HC_Server.ObserveChange(1);
        }

        void Depart()
        {
            Console.WriteLine($"{ClockTime}\t Departed.");

            S--;
            HC_Server.ObserveChange(-1);
            if (Q > 0) Start();
        }

        public MySimModel()
        {
            Schedule(Arrive);
            HC_Queue = AddHourCounter();
            HC_Server = AddHourCounter();
        }

        [JsonConstructor]
        public MySimModel(int dummy)
        {
            Dummy = dummy;
        }
    }
}
