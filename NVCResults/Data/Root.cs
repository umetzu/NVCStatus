namespace NVCResults.Data
{
    public class NvcCaseInfo
    {
        public string caseNumber { get; set; }
        public string status { get; set; }
        public string createdDate { get; set; }
        public string lastUpdatedDate { get; set; }
        public string location { get; set; }
        public string applicationType { get; set; }
        public string message { get; set; }
        public string estimatedDate =>
             new DateTime(2020, 1, 1).AddDays(int.Parse(caseNumber.Substring(7, 3)) - 501).ToString("dd-MMM-yyyy");

        public string daysPassed
        {
            get
            {
                if (lastUpdatedDate != null && createdDate != null)
                {
                    return (DateTime.Parse(lastUpdatedDate) - DateTime.Parse(createdDate)).TotalDays + "";
                }
                else
                {
                    return "";
                }
            }
        }
    }

    public class Root
    {
        public NvcCaseInfo nvcCaseInfo { get; set; }
        public bool error { get; set; }
        public string errorMessage { get; set; }
    }
}