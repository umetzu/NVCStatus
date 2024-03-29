using System.Diagnostics;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace NVCResults.Data
{
    public class NVCForecastService
    {
        public List<string> GetLogs()
        {
            var result = new List<string>();

            var nvcLogPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs");

            result = Directory.GetFiles(nvcLogPath).Select(x => Path.GetFileNameWithoutExtension(x)).ToList();

            return result;
        }

        public event Action? OnChange;
        private bool refreshingLogs;
        public bool RefreshingLogs
        {
            get => refreshingLogs; private set
            {
                refreshingLogs = value;
                OnChange?.Invoke();
            }
        }

        public async Task<bool> RefreshLogs()
        {
            if (RefreshingLogs)
            {
                return false;
            }

            try
            {
                RefreshingLogs = true;

                var startInfo = new ProcessStartInfo()
                {
                    FileName = "/bin/bash",
                    Arguments = string.Format("-c \"sudo {0} {1}\"", "/bin/pwsh", "/var/www/nvc-umetzu/Tools/nvc.ps1"),
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, 
                };

                Process process = new()
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, data) => {
                    Console.WriteLine(data.Data);
                };

                process.ErrorDataReceived += (sender, data) => {
                    Console.WriteLine(data.Data);
                };

                process.Exited += (sender, args) => { RefreshingLogs = false; };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                RefreshingLogs = false;
            }

            return false;
        }

        public SortedDictionary<string, (int, int, int)> GetSummary(List<Root> log)
        {
            var result = new SortedDictionary<string, (int, int, int)>();

            var previous = log.GroupBy(x => x.nvcCaseInfo?.previousStatus).Select(x => new { status = x?.Key, count = x?.Count() }).OrderBy(x => x.status);
            var current = log.GroupBy(x => x.nvcCaseInfo?.status).Select(x => new { status = x?.Key, count = x?.Count() }).OrderBy(x => x.status);
            var next = log.GroupBy(x => x.nvcCaseInfo?.nextStatus).Select(x => new { status = x?.Key, count = x?.Count() }).OrderBy(x => x.status);

            foreach (var item in previous)
            {
                if (!string.IsNullOrEmpty(item.status))
                {
                    result.TryGetValue(item.status ?? "", out (int, int, int) value);
                    value.Item1 = item.count ?? 0;
                    result[item.status ?? ""] = value;
                }
            }

            foreach (var item in current)
            {
                if (!string.IsNullOrEmpty(item.status))
                {
                    result.TryGetValue(item.status ?? "", out (int, int, int) value);
                    value.Item2 = item.count ?? 0;
                    result[item.status ?? ""] = value;
                }
            }

            foreach (var item in next)
            {
                if (!string.IsNullOrEmpty(item.status))
                {
                    result.TryGetValue(item.status ?? "", out (int, int, int) value);
                    value.Item3 = item.count ?? 0;
                    result[item.status ?? ""] = value;
                }
            }

            result["Total"] = (log.Count(x => !string.IsNullOrEmpty(x.nvcCaseInfo.previousStatus)),
                log.Count(x => !string.IsNullOrEmpty(x.nvcCaseInfo.status)),
                log.Count(x => !string.IsNullOrEmpty(x.nvcCaseInfo.nextStatus)));

            return result;
        }

        public Task<List<Root>> GetForecastAsync(string dateLog, List<string> logs)
        {
            int dateLogIndex = logs.IndexOf(dateLog);

            List<Root> currentLog = ReadLog(dateLog);

            string nextDateLog = "", previousDateLog = "";

            if (dateLogIndex < logs.Count - 1)
            {
                nextDateLog = logs[dateLogIndex + 1];
                var nextLog = ReadLog(nextDateLog);

                List<Root> toAdd = new List<Root>();

                foreach (var next in nextLog)
                {
                    Root? current = currentLog.FirstOrDefault(x => x.nvcCaseInfo.caseNumber == next.nvcCaseInfo.caseNumber);

                    if (current == null)
                    {
                        current = next;
                        current.nvcCaseInfo.nextStatus = current.nvcCaseInfo.status;
                        current.nvcCaseInfo.status = "";
                        toAdd.Add(current);
                    }
                    else
                    {
                        current.nvcCaseInfo.nextStatus = next.nvcCaseInfo.status;
                        current.nvcCaseInfo.createdDate ??= next.nvcCaseInfo.createdDate;
                        current.nvcCaseInfo.lastUpdatedDate = next.nvcCaseInfo.lastUpdatedDate;
                    }
                }

                currentLog.AddRange(toAdd);
            }

            if (dateLogIndex  - 1 >= 0)
            {
                previousDateLog = logs[dateLogIndex - 1];
                var previousLog = ReadLog(previousDateLog);

                List<Root> toAdd = new List<Root>();

                foreach (var previous in previousLog)
                {
                    Root? current = currentLog.FirstOrDefault(x => x.nvcCaseInfo.caseNumber == previous.nvcCaseInfo.caseNumber);

                    if (current == null)
                    {
                        current = previous;
                        current.nvcCaseInfo.previousStatus = current.nvcCaseInfo.status;
                        current.nvcCaseInfo.status = "";
                        toAdd.Add(current);
                    }
                    else
                    {
                        current.nvcCaseInfo.previousStatus = previous.nvcCaseInfo.status;
                        current.nvcCaseInfo.createdDate ??= previous.nvcCaseInfo.createdDate;
                        current.nvcCaseInfo.lastUpdatedDate = previous.nvcCaseInfo.lastUpdatedDate;
                    }
                }

                currentLog.AddRange(toAdd);
            }

            return Task.FromResult(currentLog.OrderBy(x => x.nvcCaseInfo.caseNumber).ToList());
        }

        private List<Root> ReadLog(string dateLog)
        {
            var results = new List<Root>();

            var nvcLogPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", $"{dateLog}.log");

            var nvcLog = System.IO.File.ReadAllLines(nvcLogPath);

            foreach (var logRow in nvcLog)
            {
                if (!string.IsNullOrWhiteSpace(logRow))
                {
                    var logItem = System.Text.Json.JsonSerializer.Deserialize<Root>(logRow);

                    if (logItem != null && logItem.nvcCaseInfo != null)
                    {
                        results.Add(logItem);
                    }
                }
            }

            results = results.OrderBy(x => x.nvcCaseInfo.caseNumber).ToList();

            return results;
        }
    }
}