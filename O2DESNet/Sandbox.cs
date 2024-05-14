using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace O2DESNet
{
    public interface ISandbox : IDisposable
    {
        int Index { get; }
        string Id { get; }
        Pointer Pointer { get; }
        int Seed { get; }        
        ISandbox Parent { get; }
        IReadOnlyList<ISandbox> Children { get; }
        DateTime ClockTime { get; }
        DateTime? HeadEventTime { get; }
        string LogFile { get; set; }
        bool DebugMode { get; set; }
        bool Run();
        bool Run(int eventCount);
        bool Run(DateTime terminate);
        bool Run(TimeSpan duration);
        bool Run(double speed);        
        bool WarmUp(DateTime till);
        bool WarmUp(TimeSpan period);        
    }

    public abstract class Sandbox<TAssets> : Sandbox
        where TAssets : IAssets
    {
        public TAssets Assets { get; private set; }
        public Sandbox(TAssets assets, int seed = 0, string id = null, Pointer pointer = new Pointer())
            : base(seed, id, pointer) { Assets = assets; }
    }

    public class Sandbox : ISandbox
    {
        [JsonProperty("_count")]
        private static int _count = 0;
        /// <summary>
        /// Unique index in sequence for all module instances 
        /// </summary>
        public int Index { get; private set; }
        /// <summary>
        /// Tag of the instance of the module
        /// </summary>
        public string Id { get; private set; }
        public Pointer Pointer { get; private set; }
        [JsonProperty("DefaultRS")]
        protected Random DefaultRS { get; private set; }
        [JsonProperty("_seed")]
        private int _seed;
        public int Seed { get { return _seed; } set { _seed = value; DefaultRS = new Random(_seed); } }

        #region Future Event List
        [JsonProperty("FutureEventList")]
        // internal SortedSet<Event> FutureEventList = new SortedSet<Event>(EventComparer.Instance);
        internal List<Event> FutureEventList = new List<Event>();
        public string PrintFEL()
        {
            string s = "";
            foreach(Event e in FutureEventList)
            {
                s+=e.Tag;
            }
            return s;
        }
        public void AddWithOrder(Action action, DateTime clockTime, string tag = null)
        {
            Event evt = new Event(this, action, clockTime, tag);
            if (FutureEventList.Count == 0)
            {
                FutureEventList.Add(evt);
            }
            else
            {
                int i = 0;
                for (i = 0; i < FutureEventList.Count; i++)
                {
                    Event e = FutureEventList[0];
                    if (e != null)
                    {
                        if (e.ScheduledTime.CompareTo(evt.ScheduledTime) > 0)
                        {
                            FutureEventList.Insert(i, evt);
                            break;
                        }
                        else if (e.ScheduledTime.CompareTo(evt.ScheduledTime) == 0)
                        {
                            if (e.Index.CompareTo(evt.Index) > 0)
                            {
                                FutureEventList.Insert(i, evt);
                                break;
                            }
                        }
                    }
                }
                if (i == FutureEventList.Count)
                {
                    FutureEventList.Add(evt);
                }
            }
        }
        /// <summary>
        /// Schedule an event to be invoked at the specified clock-time
        /// </summary>
        protected void Schedule(Action action, DateTime clockTime, string tag = null)
        {
            // FutureEventList.Add(new Event(this, action, clockTime, tag));
            AddWithOrder(action, clockTime, tag);
        }
        /// <summary>
        /// Schedule an event to be invoked after the specified time delay
        /// </summary>
        protected void Schedule(Action action, TimeSpan delay, string tag = null)
        {
            // FutureEventList.Add(new Event(this, action, ClockTime + delay, tag));
            AddWithOrder(action, ClockTime + delay, tag);
        }
        /// <summary>
        /// Schedule an event at the current clock time.
        /// </summary>
        protected void Schedule(Action action, string tag = null)
        {
            // FutureEventList.Add(new Event(this, action, ClockTime, tag));
            AddWithOrder(action, ClockTime, tag);
        }
        #endregion

        #region Simulation Run Control
        internal Event HeadEvent
        {
            get
            {
                var headEvent = FutureEventList.FirstOrDefault();
                foreach (Sandbox child in Children_List)
                {
                    var childHeadEvent = child.HeadEvent;
                    if (headEvent == null || (childHeadEvent != null &&
                        EventComparer.Instance.Compare(childHeadEvent, headEvent) < 0))
                        headEvent = childHeadEvent;
                }
                return headEvent;
            }
        }
        public Event GetHeadEvent() { return HeadEvent; }
        [JsonProperty("_clockTime")]
        private DateTime _clockTime = DateTime.MinValue;
        public DateTime ClockTime
        {
            get
            {
                if (Parent == null) return _clockTime;
                return Parent.ClockTime;
            }
        }
        public DateTime? HeadEventTime
        {
            get
            {
                var head = HeadEvent;
                if (head == null) return null;
                return head.ScheduledTime;
            }
        }
        public bool Run()
        {
            if (Parent != null) 
                return Parent.Run();
            var head = HeadEvent;
            if (head == null) 
                return false;
            head.Owner.FutureEventList.Remove(head);
            _clockTime = head.ScheduledTime;
            head.Invoke();
            return true;
        }
        public bool Run(TimeSpan duration)
        {
            if (Parent != null) return Parent.Run(duration);
            return Run(ClockTime.Add(duration));
        }
        public bool Run(DateTime terminate)
        {
            if (Parent != null) return Parent.Run(terminate);
            while (true)
            {
                var head = HeadEvent;
                if (HeadEvent != null && HeadEvent.ScheduledTime <= terminate) Run();
                else
                {
                    _clockTime = terminate;
                    return head != null; /// if the simulation can be continued
                }
            }
        }
        public bool Run(int eventCount)
        {
            if (Parent != null) 
                return Parent.Run(eventCount);
            while (eventCount-- > 0)
                if (!Run()) 
                    return false;
            return true;
        }
        [JsonProperty("_realTimeForLastRun")]
        private DateTime? _realTimeForLastRun = null;
        public bool Run(double speed)
        {
            if (Parent != null) return Parent.Run(speed);
            var rtn = true;
            if (_realTimeForLastRun != null)
                rtn = Run(terminate: ClockTime.AddSeconds((DateTime.Now - _realTimeForLastRun.Value).TotalSeconds * speed));
            _realTimeForLastRun = DateTime.Now;
            return rtn;
        }
        #endregion

        #region Children - Sub-modules
        public ISandbox Parent { get; private set; } = null;
        [JsonProperty("Children_List")]
        private readonly List<ISandbox> Children_List = new List<ISandbox>();
        public IReadOnlyList<ISandbox> Children { get { return Children_List.AsReadOnly(); } }
        protected TSandbox AddChild<TSandbox>(TSandbox child) where TSandbox : Sandbox
        {
            Children_List.Add(child);
            child.Parent = this;
            OnWarmedUp += child.OnWarmedUp;
            return child;
        }
        protected IReadOnlyList<HourCounter> HourCounters { get { return HourCounters_List.AsReadOnly(); } }
        [JsonProperty("HourCounters_List")]
        private readonly List<HourCounter> HourCounters_List = new List<HourCounter>();
        protected HourCounter AddHourCounter(bool keepHistory = false)
        {
            var hc = new HourCounter(this, keepHistory);
            HourCounters_List.Add(hc);
            OnWarmedUp += () => hc.WarmedUp();
            return hc;
        }
        #endregion
        
        public Sandbox(int seed = 0, string id = null, Pointer pointer = new Pointer())
        {
            Seed = seed;
            Index = ++_count;
            Id = id;
            Pointer = pointer;
            OnWarmedUp += WarmedUpHandler;
        }
        
        [JsonConstructor]
        public Sandbox(int index, string id, Pointer pointer, Random defaultRS, ISandbox parent) 
        {
            Index = index;
            Id = id;
            Pointer = pointer;
            DefaultRS = defaultRS;
            Parent = parent;
        }

        public override string ToString()
        {
            var str = Id;
            if (str == null || str.Length == 0) str = GetType().Name;
            str += "#" + Index.ToString();
            return str;
        }

        #region Warm-Up
        public bool WarmUp(TimeSpan period)
        {
            if (Parent != null) return Parent.WarmUp(period);
            return WarmUp(ClockTime + period);
        }
        public bool WarmUp(DateTime till)
        {
            if (Parent != null) return Parent.WarmUp(till);
            var result = Run(till);
            OnWarmedUp.Invoke();
            return result; // to be continued
        }
        private Action OnWarmedUp;
        protected virtual void WarmedUpHandler() { }
        #endregion

        #region For Logging
        [JsonProperty("_logFile")]
        private string _logFile;
        public string LogFile
        {
            get { return _logFile; }
            set
            {
                _logFile = value; if (_logFile != null) using (var sw = new StreamWriter(_logFile)) { };
            }
        }
        protected void Log(params object[] args)
        {
            var timeStr = ClockTime.ToString("y/M/d H:mm:ss.fff");
            if (LogFile != null)
            {
                using (var sw = new StreamWriter(LogFile, true))
                {
                    sw.Write("{0}\t{1}\t", timeStr, Id);
                    foreach (var arg in args) sw.Write("{0}\t", arg);
                    sw.WriteLine();
                }
            }
        }

        public bool DebugMode { get; set; } = false;
        #endregion

        public virtual void Dispose()
        {
            foreach (var child in Children_List) child.Dispose();
            foreach (var hc in HourCounters_List) hc.Dispose();
        }
    }
}
