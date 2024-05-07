using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace O2DESNet
{
    public interface IReadOnlyHourCounter
    {
        DateTime LastTime { get; }
        double LastCount { get; }
        bool Paused { get; }
        /// <summary>
        /// Total number of increment observed
        /// </summary>
        double TotalIncrement { get; }
        /// <summary>
        /// Total number of decrement observed
        /// </summary>
        double TotalDecrement { get; }
        double IncrementRate { get; }
        double DecrementRate { get; }
        /// <summary>
        /// Total number of hours since the initial time.
        /// </summary>
        double TotalHours { get; }
        double WorkingTimeRatio { get; }
        /// <summary>
        /// The cumulative count value on time in unit of hours
        /// </summary>
        double CumValue { get; }
        /// <summary>
        /// The average count on observation period
        /// </summary>
        double AverageCount { get; }
        /// <summary>
        /// Average timespan that a load stays in the activity, if it is a stationary process, 
        /// i.e., decrement rate == increment rate
        /// It is 0 at the initial status, i.e., decrement rate is NaN (no decrement observed).
        /// </summary>
        TimeSpan AverageDuration { get; }
        string LogFile { get; set; }
    }
    public interface IHourCounter : IReadOnlyHourCounter
    {
        void ObserveCount(double count, DateTime clockTime);
        void ObserveChange(double count, DateTime clockTime);
        void Pause();
        void Pause(DateTime clockTime);
        void Resume(DateTime clockTime);
    }
    public class ReadOnlyHourCounter : IReadOnlyHourCounter, IDisposable
    {
        public DateTime LastTime { get { return HourCounter.LastTime; } }

        public double LastCount { get { return HourCounter.LastCount; } }

        public bool Paused { get { return HourCounter.Paused; } }

        public double TotalIncrement { get { return HourCounter.TotalIncrement; } }

        public double TotalDecrement { get { return HourCounter.TotalDecrement; } }

        public double IncrementRate { get { return HourCounter.IncrementRate; } }

        public double DecrementRate { get { return HourCounter.DecrementRate; } }

        public double TotalHours { get { return HourCounter.TotalHours; } }

        public double WorkingTimeRatio { get { return HourCounter.WorkingTimeRatio; } }

        public double CumValue { get { return HourCounter.CumValue; } }

        public double AverageCount { get { return HourCounter.AverageCount; } }

        public TimeSpan AverageDuration { get { return HourCounter.AverageDuration; } }
        public string LogFile
        {
            get { return HourCounter.LogFile; }
            set { HourCounter.LogFile = value; }
        }

        private readonly HourCounter HourCounter;
        internal ReadOnlyHourCounter(HourCounter hourCounter)
        {
            HourCounter = hourCounter;
        }

        public void Dispose() { }
    }
    public class HourCounter : IHourCounter, IDisposable
    {
        [JsonProperty("_sandbox")]
        private Sandbox _sandbox;
        [JsonProperty("_initialTime")]
        private DateTime _initialTime;
        public DateTime LastTime { get; private set; }
        public double LastCount { get; private set; }
        /// <summary>
        /// Total number of increment observed
        /// </summary>
        public double TotalIncrement { get; private set; }
        /// <summary>
        /// Total number of decrement observed
        /// </summary>
        public double TotalDecrement { get; private set; }
        /// <summary>
        /// Total number of hours since the initial time.
        /// </summary>
        public double TotalHours { get; private set; }
        private void UpdateToClockTime()
        {
            if (LastTime != _sandbox.ClockTime) ObserveCount(LastCount);
        }
        public double WorkingTimeRatio
        {
            get
            {
                UpdateToClockTime();
                if (LastTime == _initialTime) return 0;
                return TotalHours / (LastTime - _initialTime).TotalHours;
            }
        }
        /// <summary>
        /// The cumulative count value (integral) on time in unit of hours
        /// </summary>
        public double CumValue { get; private set; }
        /// <summary>
        /// The average count on observation period
        /// </summary>
        public double AverageCount
        {
            get
            {
                UpdateToClockTime();
                if (TotalHours == 0) return LastCount; return CumValue / TotalHours;
            }
        }
        /// <summary>
        /// Average timespan that a load stays in the activity, if it is a stationary process, 
        /// i.e., decrement rate == increment rate
        /// It is 0 at the initial status, i.e., decrement rate is NaN (no decrement observed).
        /// </summary>
        public TimeSpan AverageDuration
        {
            get
            {
                UpdateToClockTime();
                double hours = AverageCount / DecrementRate;
                if (double.IsNaN(hours) || double.IsInfinity(hours)) hours = 0;
                return TimeSpan.FromHours(hours);
            }
        }
        public bool Paused { get; private set; }

        #region For history keeping
        [JsonProperty("_history")]
        private Dictionary<DateTime, double> _history;
        public bool KeepHistory { get; private set; }
        /// <summary>
        /// Scatter points of (time in hours, count)
        /// </summary>
        public List<Tuple<double, double>> History
        {
            get
            {
                if (!KeepHistory) return null;
                return _history.OrderBy(i => i.Key).Select(i => new Tuple<double, double>((i.Key - _initialTime).TotalHours, i.Value)).ToList();
            }
        }
        #endregion

        HourCounter() { }
        [JsonConstructor]
        public HourCounter(DateTime lastTime, double lastCount, double totalIncrement, double totalDecrement, 
                            double totalHours, double cumValue, bool paused, bool keepHistory)
        {
            LastTime = lastTime;
            LastCount = lastCount;
            TotalIncrement = totalIncrement;
            TotalDecrement = totalDecrement;
            TotalHours = totalHours;
            CumValue = cumValue;
            Paused = paused;
            KeepHistory = keepHistory;
        }
        internal HourCounter(Sandbox sandbox, bool keepHistory = false)         
        {
            Init(sandbox, DateTime.MinValue, keepHistory); 
        }
        internal HourCounter(Sandbox sandbox, DateTime initialTime, bool keepHistory = false) 
        {
            Init(sandbox, initialTime, keepHistory); 
        }
        private void Init(Sandbox sandbox, DateTime initialTime, bool keepHistory)
        {
            _sandbox = sandbox;
            _initialTime = initialTime;
            LastTime = initialTime;
            LastCount = 0;
            TotalIncrement = 0;
            TotalDecrement = 0;
            TotalHours = 0;
            CumValue = 0;
            KeepHistory = keepHistory;
            if (KeepHistory) _history = new Dictionary<DateTime, double>();
        }
        public void ObserveCount(double count)
        {
            var clockTime = _sandbox.ClockTime;
            // The following check is disabled due to deserialization requirement
            // if (clockTime < LastTime)
            //     throw new Exception("Time of new count cannot be earlier than current time.");
            if (!Paused)
            {
                var hours = (clockTime - LastTime).TotalHours;
                TotalHours += hours;
                CumValue += hours * LastCount;
                if (count > LastCount) TotalIncrement += count - LastCount;
                else TotalDecrement += LastCount - count;
            }
            if (_logFile != null)
            {
                using (var sw = new StreamWriter(_logFile, append: true))
                {
                    sw.Write("{0},{1}", TotalHours, LastCount);
                    if (Paused) sw.Write(",Paused");
                    sw.WriteLine();
                    if (count != LastCount)
                    {
                        sw.Write("{0},{1}", TotalHours, count);
                        if (Paused) sw.Write(",Paused");
                        sw.WriteLine();
                    }
                };
            }
            LastTime = clockTime;
            LastCount = count;
            if (KeepHistory) _history[clockTime] = count;
        }
        /// <summary>
        /// Remove parameter clockTime as since Version 3.6, according to Issue 1
        /// </summary>
        public void ObserveCount(double count, DateTime clockTime)
        {
            CheckClockTime(clockTime);
            ObserveCount(count);
        }
        public void ObserveChange(double change) { ObserveCount(LastCount + change); }
        /// <summary>
        /// Remove parameter clockTime as since Version 3.6, according to Issue 1
        /// </summary>
        public void ObserveChange(double change, DateTime clockTime) 
        {
            CheckClockTime(clockTime);
            ObserveChange(change); 
        }
        public void Pause() 
        {
            var clockTime = _sandbox.ClockTime;
            if (Paused) return;
            ObserveCount(LastCount, clockTime);
            Paused = true;
            if (_logFile != null)
            {
                using (var sw = new StreamWriter(_logFile, append: true))
                {
                    sw.WriteLine("{0},{1},Paused", TotalHours, LastCount);
                };
            }
        }
        /// <summary>
        /// Remove parameter clockTime as since Version 3.6, according to Issue 1
        /// </summary>
        public void Pause(DateTime clockTime)
        {
            CheckClockTime(clockTime);
            Pause();
        }
        public void Resume()
        {
            if (!Paused) return;
            LastTime = _sandbox.ClockTime;
            Paused = false;
            if (_logFile != null)
            {
                using (var sw = new StreamWriter(_logFile, append: true))
                {
                    sw.WriteLine("{0},{1},Paused", TotalHours, LastCount);
                    sw.WriteLine("{0},{1}", TotalHours, LastCount);
                };
            }
        }
        /// <summary>
        /// Remove parameter clockTime as since Version 3.6, according to Issue 1
        /// </summary>
        public void Resume(DateTime clockTime)
        {
            CheckClockTime(clockTime);
            Resume();
        }
        private void CheckClockTime(DateTime clockTime)
        {
            if (clockTime != _sandbox.ClockTime) throw new Exception("ClockTime is not consistent with the Sandbox.");
        }

        public double IncrementRate
        {
            get
            {
                UpdateToClockTime();
                return TotalIncrement / TotalHours;
            }
        }
        public double DecrementRate
        {
            get
            {
                UpdateToClockTime();
                return TotalDecrement / TotalHours;
            }
        }
        internal void WarmedUp()
        {

            // all reset except the last count
            _initialTime = _sandbox.ClockTime;
            LastTime = _sandbox.ClockTime;
            TotalIncrement = 0;
            TotalDecrement = 0;
            TotalHours = 0;
            CumValue = 0;
        }
        
        [JsonProperty("_logFile")]
        private string _logFile;
        public string LogFile
        {
            get { return _logFile; }
            set
            {
                _logFile = value;
                if (_logFile != null)
                    using (var sw = new StreamWriter(_logFile))
                    {
                        sw.WriteLine("Hours,Count,Remark");
                        sw.WriteLine("{0},{1}", TotalHours, LastCount);
                    };
            }
        }

        [JsonProperty("ReadOnly")]
        private ReadOnlyHourCounter ReadOnly { get; set; } = null;
        public ReadOnlyHourCounter AsReadOnly()
        {
            if (ReadOnly == null) ReadOnly = new ReadOnlyHourCounter(this);
            return ReadOnly;
        }

        public void Dispose() { }
    }
}
