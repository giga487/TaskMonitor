using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskMonitor.Common;
using TaskMonitor.Utility;

namespace TaskMonitor
{
    public class TaskMonitor
    {
        public int TimeMSToShow { get; private set; } = 60000;
        public Serilog.ILogger? Logger { get; set; } = null;
        public string MonitorName { get; set; } = "Default monitor";
        private List<int> ColumnsWidth { get; set; } = new List<int>()
        {
            20,
            40,
            10,
            10,
            14
        };
        public TaskMonitor(int millisecondForPolling, Serilog.ILogger logger)
        {
            TimeMSToShow = millisecondForPolling;

            foreach (TypeOfTask t in Enum.GetValues(typeof(TypeOfTask)))
            {
                _processAverageTime[t] = new();

            }

            Logger = logger;
        }
        public record TaskObject
        {
            public Task? Task { get; set; }
            public int UnivoqueID { get; set; }
            public string? CreatedBy { get; set; }
            //public Task? Task { get; set; } = null;
            public TypeOfTask Type { get; set; } = TypeOfTask.Default;
            public string MethodReq { get; set; } = string.Empty;
        }

        public record TaskTypeInfo
        {
            /// <summary>
            /// AVERAGE WINDOWED VALUE
            /// </summary>
            public static int MaxItems = 100;
            private LimitedQueue<double> MemorizedValues { get; set; }
            public int Count { get; set; } = 0;
            private double _average = 0;
            public double Average => _average;
            public TaskTypeInfo()
            {
                MemorizedValues = new LimitedQueue<double>(MaxItems);
            }
            public void AddNewTicks(double milliseconds)
            {
                MemorizedValues.Add(milliseconds);

                try
                {
                    _average = MemorizedValues.GetSumOrList() / MemorizedValues.Count; /// MemorizedValues.Count;
                }
                catch (Exception e)
                {

                }

                Count++;
            }

            private double _lastAverage = 0;
            public double LastAverage => _lastAverage;
            public double PercentageChange => (100 * (_average - _lastAverage) / _average);

            public void SetLast()
            {
                _lastAverage = _average;
            }
        }

        private ConcurrentDictionary<int, TaskObject> _tasks = new ConcurrentDictionary<int, TaskObject>();
        private ConcurrentDictionary<TypeOfTask, ConcurrentDictionary<string, TaskTypeInfo>> _processAverageTime { get; set; } = new ConcurrentDictionary<TypeOfTask, ConcurrentDictionary<string, TaskTypeInfo>>();

        private string FillText(char t, int columnIndex)
        {
            return new string(t, ColumnsWidth[columnIndex]);
        }
        private string PadRightText(string text, int columnIndex)
        {
            var stringToShow = text;

            int maxLength = ColumnsWidth[columnIndex];
            try
            {
                if (stringToShow.Length > maxLength)
                {
                    int startedIndex = stringToShow.Length - maxLength + 3;
                    stringToShow = "..." + stringToShow.Substring(startedIndex);
                }
                stringToShow = stringToShow.PadRight(maxLength);
            }
            catch (Exception)
            {
                stringToShow = text;
            }

            return stringToShow;
        }

        public async void ShowMonitoredObject(CancellationToken token)
        {
            Logger?.ForContext(Serilog.Core.Constants.SourceContextPropertyName, MonitorName);

            int i = 0;
            while (!token.IsCancellationRequested)
            {
                var ts = Stopwatch.StartNew();

                string text = $"\n########################### TABLE FOR TASK MONITOR ############################\n";
                text += $"########## Active task: [{_tasks.Count}],  Monitored task average process ticks, WINDOWED ITEMS FOR AVG: {TaskTypeInfo.MaxItems} ######\n";
                text += $"{PadRightText("Type", 0)}| {PadRightText("Method", 1)}| {PadRightText("MilliS", 2)}| {PadRightText("Changed %", 3)}| {PadRightText("Closed Task", 4)}|\n";

                foreach (var typeTask in _processAverageTime)
                {
                    foreach (var requestMethodKV in typeTask.Value)
                    {
                        if (requestMethodKV.Value is null)
                        {
                            continue;
                        }

                        text += $"{PadRightText(typeTask.Key.ToString(), 0)}| {PadRightText(requestMethodKV.Key, 1)}| {PadRightText(requestMethodKV.Value.Average.ToString("F3"), 2)}| {PadRightText(requestMethodKV.Value.PercentageChange.ToString("+000.00;-000.00;000.00"), 3)}| {PadRightText(requestMethodKV.Value.Count.ToString(), 4)}|\n";
                        requestMethodKV.Value.SetLast();
                    }
                }

                text += $"{FillText('_', 0)}| {FillText('_', 1)}| {FillText('_', 2)}| {FillText('_', 3)}| {FillText('_', 4)}|\n";

                ts.Stop();
                text += $"Table creation time {ts.ElapsedMilliseconds}ms";
                Logger?.Information(text);


                await Task.Delay(TimeMSToShow);
            }
        }


        public void AddTask(IEnumerable<Task> tasks, string createdBy, TypeOfTask type, string methodRequest = "")
        {
            Parallel.ForEach(tasks, new ParallelOptions() { MaxDegreeOfParallelism = 100 }, task =>
            {
                AddTask(task, createdBy, type, methodRequest);
            });

        }
        public volatile int LastId = 0;
        public ConcurrentBag<int> Removed = new ConcurrentBag<int>();
        public void AddTask(Task? task, string createdBy, TypeOfTask type, string methodRequest = "")
        {
            if (task is null)
            {
                return;
            }

            var taskCreation = DateTime.UtcNow;
            //task.ContinueWith(t => MeasureTime(t, taskCreation));

            TaskObject taskObject = new TaskObject()
            {
                UnivoqueID = LastId++,
                Task = task,
                CreatedBy = createdBy,
                Type = type,
                MethodReq = methodRequest
            };

            //task.ContinueWith(t => MeasureTime(t, taskCreation));
            _tasks.TryAdd(taskObject.UnivoqueID, taskObject);

            task.ContinueWith(t =>
            {
                double milliseconds = Math.Round((DateTime.UtcNow - taskCreation).TotalMilliseconds, 3);

                if (!t.IsCompleted)
                {
                    return;
                }

                if (!_tasks.TryRemove(taskObject.UnivoqueID, out var taskObj))
                {
                    Logger?.Warning($"Can't remove {t.Id}");
                    return;
                }
                else
                {
                    Removed.Add(t.Id);
                    //t.Dispose();
                }

                if (!_processAverageTime[taskObj.Type].TryGetValue(taskObj.MethodReq, out var request))
                {
                    _processAverageTime[taskObj.Type][taskObj.MethodReq] = new TaskTypeInfo();
                }
                if (_processAverageTime[taskObj.Type].ContainsKey(taskObj.MethodReq))
                    _processAverageTime[taskObj.Type][taskObj.MethodReq].AddNewTicks(milliseconds);
            });

        }

        private void MeasureTime(Task t, DateTime creation)
        {
            double milliseconds = Math.Round((DateTime.UtcNow - creation).TotalMilliseconds, 3);
            //long ticksAfter = DateTime.UtcNow - creation;
            if (!_tasks.TryRemove(t.Id, out var taskObj))
            {
                Logger?.Warning($"Can't remove {t.Id}");
                return;
            }
            else
            {
                //t.Dispose();
            }

            if (!_processAverageTime[taskObj.Type].TryGetValue(taskObj.MethodReq, out var request))
            {
                _processAverageTime[taskObj.Type][taskObj.MethodReq] = new TaskTypeInfo();
            }
            else
            {
                request.AddNewTicks(milliseconds);
            }

        }

        private void MeasureTime(Task t)
        {

        }

        public int GetActiveTaskCount()
        {
            return _tasks.Count;
        }
    }

}
