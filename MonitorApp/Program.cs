using Serilog;
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

            TaskMonitor.TaskMonitor tMon = new TaskMonitor.TaskMonitor(1000, Logger, "Tasks");
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tMon.ShowMonitoredObject(tokenSource.Token);

            TaskTest test = new TaskTest(Logger, tMon);

            List<Task> tasks = new List<Task>();    
            Task t = new Task(() => test.Count(100));

            tMon.AddTask(t, "Monitor", TaskMonitor.Common.TypeOfTask.Task1, "Req Count");
            t.Start();

            Task t2 = new Task(() => test.CreatingLotsOfLittleTasks(10_000));
            tMon.AddTask(t2, "Monitor", TaskMonitor.Common.TypeOfTask.Task2, "Creating lots of tasks");
            t2.Start();

            Logger.Information("Waiting for all the tasks closed!");
            Task.WaitAll(tasks.ToArray());

            Logger.Information("Closing the execution");

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

            var tasks = Enumerable.Range(0, numberOfTasks).Select(async x =>
            {
                int valueToReach = rnd.Next(5000, 50000);
                var t = LittleTask(valueToReach);
                _tMon.AddTask(t, $"CreatedBy", TaskMonitor.Common.TypeOfTask.Task2, $"Little task");

                await t;

                if(x % 1000 == 1)
                {
                    _logger?.Information($"Created {x}/{numberOfTasks} tasks");
                }
            });

            Task.WaitAll(tasks.ToArray());
        }

        public void CreatingLotsOfLittleTasks(int numberOfTasks)
        {
            Random rnd = new Random();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < numberOfTasks; i++)
            {
                int valueToReach = rnd.Next(5000, 50000);
                var t = LittleTask(valueToReach);
                _tMon.AddTask(t, $"CreatedBy", TaskMonitor.Common.TypeOfTask.Task2, $"Little task");
                tasks.Add(t);

                if (i % 1000 == 1)
                {
                    _logger?.Information($"Created {i}/{numberOfTasks} tasks");
                }
            }

            //Thread.Sleep(5000);
            Task.WaitAll(tasks.ToArray());

            var faultedTasks = tasks.Where(x => x.IsFaulted || x.IsCanceled).ToList();
            var okTasks = tasks.Where(p => p.IsCompletedSuccessfully).ToList();
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
            }, TaskCreationOptions.DenyChildAttach);

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
