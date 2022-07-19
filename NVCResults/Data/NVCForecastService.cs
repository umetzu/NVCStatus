using static System.Net.WebRequestMethods;

namespace NVCResults.Data
{
    public class NVCForecastService
    {
        public Task<List<Root>> GetForecastAsync(string dateLog)
        {
            var results = new List<Root>();

            var suffix = dateLog == "07/12/2022" ? "" : "-1";

            var nvcLogPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", $"nvc{suffix}.log");

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