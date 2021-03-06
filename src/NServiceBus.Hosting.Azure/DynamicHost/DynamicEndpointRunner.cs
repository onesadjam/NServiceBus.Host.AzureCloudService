namespace NServiceBus.Hosting.Azure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Logging;

    class DynamicEndpointRunner
    {
        public bool RecycleRoleOnError { get; set; }

        public int TimeToWaitUntilProcessIsKilled { get; set; }

        public void Start(IEnumerable<EndpointToHost> toHost)
        {
            foreach (var service in toHost)
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo(service.EntryPoint)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    var process = new Process
                    {
                        StartInfo = processStartInfo,
                        EnableRaisingEvents = true
                    };
                    var processWasKilledOnPurpose = false;

                    process.ErrorDataReceived += (o, args) =>
                    {
                        logger.Error(args.Data);
                        if (process.ExitCode != 0 && !processWasKilledOnPurpose)
                        {
                            if (RecycleRoleOnError) SafeRoleEnvironment.RequestRecycle();
                        }
                    };

                    process.OutputDataReceived += (o, args) => logger.Debug(args.Data);

                    process.Exited += (o, args) =>
                    {
                        bool trash;
                        processWasKilledOnPurpose = StoppedProcessIds.TryRemove(process.Id, out trash);
                        if (process.ExitCode != 0 && !processWasKilledOnPurpose)
                        {
                            if (RecycleRoleOnError) SafeRoleEnvironment.RequestRecycle();
                        }
                    };

                    process.Start();

                    service.ProcessId = process.Id;

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception e)
                {
                    logger.Error(e.Message);

                    if (RecycleRoleOnError) SafeRoleEnvironment.RequestRecycle();
                }
            }
        }

        public void Stop(IEnumerable<EndpointToHost> runningServices)
        {
            foreach (var runningService in runningServices)
            {
                if (runningService.ProcessId == 0) continue;

                KillProcess(runningService.ProcessId, TimeToWaitUntilProcessIsKilled);

                runningService.ProcessId = 0;
            }
        }

        internal static void KillProcess(int processId, int timeToWaitUntilProcessIsKilled)
        {
            var process = Process
                .GetProcesses()
                .FirstOrDefault(x => x.Id == processId);
            if (process == null)
            {
                return;
            }
            StoppedProcessIds.TryAdd(processId, true);
            process.Kill();
            //As per MSDN "The Kill method executes asynchronously. After calling the Kill method, call the WaitForExit method to wait for the process to exit" 
            if (!process.WaitForExit(timeToWaitUntilProcessIsKilled))
            {
                throw new Exception($"Unable to kill process {process.ProcessName}");
            }
        }

        ILog logger = LogManager.GetLogger(typeof(DynamicEndpointRunner));
        static ConcurrentDictionary<int, bool> StoppedProcessIds = new ConcurrentDictionary<int, bool>();
    }
}