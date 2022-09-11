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