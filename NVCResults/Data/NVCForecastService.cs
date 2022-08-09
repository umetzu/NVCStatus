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

        public Task<List<Root>> GetForecastAsync(string dateLog)
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

            return Task.FromResult(results);
        }
    }
}