using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;
using TaskMonitor;

namespace MonitorApp
{
    internal class Program
    {
        static  Serilog.ILogger? Logger { get; set; }
        static void Main(string[] args)
        {
            Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate: "{Timestamp:HH:mm} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}").CreateLogger();
            Logger = Logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Default");
            Logger.Information("Hello, World!");

            TaskMonitor.TaskMonitor tMon = new TaskMonitor.TaskMonitor(10000, Logger, "Tasks");
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tMon.ShowMonitoredObject(tokenSource.Token);

            TaskTest test = new TaskTest(Logger, tMon);

            Stopwatch stopwatch = Stopwatch.StartNew();
            test.CreatingLotsOfLittleTasksParallelForEach(120_000);
            Logger?.Information($"#####  Executed parallel for each {stopwatch.ElapsedMilliseconds} #########");


            stopwatch.Restart();
            test.CreatingLotsOfCount(120_000);
            Logger?.Information($"#####  Executed count in {stopwatch.ElapsedMilliseconds} #########");


            //List<Task> tasks = new List<Task>();    
            //Task t = new Task(() => test.Count(1000));

            //tMon.AddTask(t, "Monitor", TaskMonitor.Common.TypeOfTask.Task1, "Req Count");
            //t.Start();

            //Task.WaitAll(t);

            Task t2 = new Task(() => test.CreatingLotsOfLittleTasks(120_000));
            tMon.AddTask(t2, "Monitor", TaskMonitor.Common.TypeOfTask.Task2, "Creating lots of tasks");
            t2.Start();

            //Task t3 = new Task(() => test.CreatingLotsOfLittleTasksSelect(1_000));
            //tMon.AddTask(t3, "Monitor", TaskMonitor.Common.TypeOfTask.Task3, "Creating lots, SELECT");
            //t3.Start();

            Logger?.Information("Waiting for all the tasks closed!");
            //Task.WaitAll(tasks.ToArray());

            Logger?.Information("Closing the execution");

            Console.ReadKey();

            tokenSource.Cancel();
        }

    }

    public class TaskTest
    {
        Serilog.ILogger? _logger { get; set; } = null;
        TaskMonitor.TaskMonitor _tMon { get; set; }
        public TaskTest(Serilog.ILogger logger, TaskMonitor.TaskMonitor taskMonitor)
        {
            _logger = logger?.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Task Test");
            _tMon = taskMonitor;
        }
        public async void CountAsync(int valueToReach)
        {
            for (int i = 0; i < valueToReach; i++)
            {
                await Task.Delay(1000);
            }

            _logger?.Information("The count is finished");
        }

        public void CreatingLotsOfLittleTasksSelect(int numberOfTasks)
        {
            Random rnd = new Random();

            var tasks = Enumerable.Range(0, numberOfTasks).Select(x =>
            {
                int valueToReach = rnd.Next(5_000_000, 5_000_000);
                var t = LittleTask(valueToReach);
                _tMon.AddTask(t, $"CreatedBy", TaskMonitor.Common.TypeOfTask.Task3, $"Little task, SELECT");

                if (x % 1000 == 1)
                {
                    _logger?.Information($"Created {x}/{numberOfTasks} tasks");
                }

                return t;
            });

            Task.WaitAll(tasks.ToArray());
        }

        public void CreatingLotsOfLittleTasksParallelForEach(int numberOfTasks)
        {
            var rnd = new ThreadLocal<Random>(() => new Random());
            List<Task> tasks = new List<Task>();


            Parallel.ForEach(Enumerable.Range(0, numberOfTasks), x =>
            {
                int i = 0;
                int valueToReach = rnd.Value.Next(5_000_000, 5_000_000);

                for (int t = 0; t < valueToReach; t++)
                {

                }

                if (i % 1000 == 1)
                {
                    _logger?.Information($"Created {i}/{numberOfTasks} tasks");
                }
                i++;
            });
        }

        public void CreatingLotsOfCount(int numberOfTasks)
        {
            Random rnd = new Random();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < numberOfTasks; i++)
            {
                int valueToReach = rnd.Next(5000, 5_000_000);
                LittleCount(valueToReach);

                if (i % 1000 == 1)
                {
                    _logger?.Information($"Created {i}/{numberOfTasks} tasks");
                }
            }

            var faultedTasks = tasks.Where(x => x.IsFaulted || x.IsCanceled).ToList();
            var okTasks = tasks.Where(p => p.IsCompletedSuccessfully).ToList();

            Task.WaitAll(tasks.ToArray());
        }
        public void CreatingLotsOfLittleTasks(int numberOfTasks)
        {
            Random rnd = new Random();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < numberOfTasks; i++)
            {
                int valueToReach = rnd.Next(5000, 5_000_000);
                var t = LittleTask(valueToReach);
                _tMon.AddTask(t, $"CreatedBy", TaskMonitor.Common.TypeOfTask.Task2, $"Little task");
                tasks.Add(t);

                if (i % 1000 == 1)
                {
                    _logger?.Information($"Created {i}/{numberOfTasks} tasks");
                }
            }

            var faultedTasks = tasks.Where(x => x.IsFaulted || x.IsCanceled).ToList();
            var okTasks = tasks.Where(p => p.IsCompletedSuccessfully).ToList();

            Task.WaitAll(tasks.ToArray());
        }
        public void LittleCount(int valueToReach)
        {
            _logger?.Debug($"Creating little task, ID: {Task.CurrentId}, value to reach {valueToReach}");
            for (int i = 0; i < valueToReach; i++)
            {

            }

            _logger?.Debug($"Closing little task, ID: {Task.CurrentId}");
        }
        public Task LittleTask(int valueToReach)
        {         
            Task t = Task.Factory.StartNew(() =>
            {
                _logger?.Debug($"Creating little task, ID: {Task.CurrentId}, value to reach {valueToReach}");
                for (int i = 0; i < valueToReach; i++)
                {

                }

                _logger?.Debug($"Closing little task, ID: {Task.CurrentId}");
            });

            return t;
        }

        public void Count(int valueToReach)
        {
            var countLogger = _logger?.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "Sync Test");
            countLogger?.Information("Starting count in sync way, blocking the execution task on every sleep");
            for (int i = 0; i < valueToReach; i++)
            {
                Thread.Sleep(1);
            }

            countLogger?.Information("The count is finished");
        }
    }
}
