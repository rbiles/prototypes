namespace MAExtensionSample
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.Tracing;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class MaExtBase
    {
        private string stopEventName = string.Empty;
        private string configBody = string.Empty;

        public string ConfigBody { get { return this.configBody;} }

        protected MaExtBase()
        {
            try
            {
                this.stopEventName = Environment.GetEnvironmentVariable("MON_EXTENSION_STOP_EVENT");
                this.configBody = Environment.GetEnvironmentVariable("MON_EXTENSION_BODY");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public class TaskInfo
        {
            public RegisteredWaitHandle Handle = null;
            public CancellationTokenSource Cts = new CancellationTokenSource();
            public EventWaitHandle StopEvent = null;
            public int timeout = 5000;
        }

        protected void Start(string[] args)
        {
            var ti = new TaskInfo();

            try
            {
                ti.StopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, this.stopEventName);

                if (string.IsNullOrEmpty(this.stopEventName))
                {
                    switch (args.Length)
                    {
                        case 1:
                            ti.timeout = ConvertToNumber(args[0]);
                            break;
                    }

                    ThreadPool.QueueUserWorkItem(new WaitCallback(DoTest), ti);
                }

                ti.Handle = ThreadPool.RegisterWaitForSingleObject(ti.StopEvent, new WaitOrTimerCallback(WaitProc), ti, -1, true);
                DoWork(ti.Cts.Token);
            }
            catch (AggregateException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static void DoTest(object obj)
        {
            TaskInfo ti = (TaskInfo)obj;
            if (ti.StopEvent != null)
            {
                Console.WriteLine("Test: Sleep({0})", ti.timeout);
                Thread.Sleep(ti.timeout);
                Console.WriteLine("Test: Setting stop event");
                ti.StopEvent.Set();
            }
        }

        private static void WaitProc(object obj, bool timedOut)
        {
            TaskInfo ti = (TaskInfo)obj;
            if (ti.Handle != null)
            {
                Console.WriteLine("Got stop event");
                ti.Handle.Unregister(null);
                ti.Cts.Cancel();
            }
        }

        protected static int ConvertToNumber(string number)
        {
            int result;
            return !Int32.TryParse(number, out result) ? 0 : result;
        }

        protected abstract void DoWork(CancellationToken ct);
    }

    class MAExtensionSample : EventSource
    {
        public static MAExtensionSample Log = new MAExtensionSample();

        public void Startup() { WriteEvent(1); }
        public void DoWork(string info) { WriteEvent(2, info); }
        public void OnStop() { WriteEvent(3); }
    }

    class Program : MaExtBase
    {
        private int Count = 0;
        static void Main(string[] args)
        {
            var me = new Program();
            if (me.Initialize())
            {
                me.Start(args);
            }
        }

        private bool Initialize()
        {
            return true;
        }

        protected override void DoWork(CancellationToken ct)
        {
            var tasks = new ConcurrentBag<Task>();
            var t = Task.Factory.StartNew(() => Run(ct), ct);
            tasks.Add(t);
            Task.WaitAll(tasks.ToArray());
        }

        private void Run(CancellationToken ct)
        {
            string name = MAExtensionSample.GetName(typeof(MAExtensionSample));
            MAExtensionSample.Log.Startup();

            for (; ; )
            {
                if (ct.IsCancellationRequested)
                {
                    MAExtensionSample.Log.OnStop();
                    Console.WriteLine("Extension execution was cancelled");
                    ct.ThrowIfCancellationRequested();
                }

                MAExtensionSample.Log.DoWork(this.ConfigBody);
                Console.WriteLine("Working, iteration #{0}", this.Count);
                this.Count++;
                Thread.Sleep(2000);
            }
        }
    }
}
