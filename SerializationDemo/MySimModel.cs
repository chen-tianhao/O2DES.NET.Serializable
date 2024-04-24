using O2DESNet;

namespace SerializationTest
{
    class MySimModel : Sandbox
    {
        int Q = 0;
        int S = 0;
        public HourCounter HC_Queue;
        public HourCounter HC_Server;

        void Arrive()
        {
            Console.WriteLine($"{ClockTime}\t Arrived.");
            // Schedule(Arrive, Exponential.Sample(DefaultRS, TimeSpan.FromHours(1.0 / 4)));
            Schedule(Arrive, TimeSpan.FromHours(1.0 / 4));

            Q++;
            HC_Queue.ObserveChange(1);
            if (S == 0) Start();
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
    }
}
