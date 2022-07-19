using AntiCaptchaAPI;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

const string fileName = "state.txt";

EdgeDriver? driver = null;
string currentDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Driver");

var currentRunTime = DateTime.Now.ToString("yyyy-MM-dd");

//LMA2020656002
//LMA2020619001
var caseNumberPre = "LMA2020";
int caseNumberDate = -1;
int caseNumberIndex = -1;

var captchaKey = "";
var fromCaseNumberDate = 600;
var toCaseNumberDate = 676;

ReadIni();
LoadState();

try
{
    driver = new EdgeDriver(currentDirectory)
    {
        Url = "https://ceac.state.gov/ceacstattracker/status.aspx"
    };

    await Task.Delay(2000);

    while (caseNumberDate <= toCaseNumberDate)
    {
        await CheckStatus();
        caseNumberDate += 1;
        SaveState();
    }
}
finally
{
    try
    {
        driver?.Quit();
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error while closing {e.Message}");
    }
}

async Task WaitForLoading()
{
    try
    {
        var elementImageLoading = driver?.FindElement(By.Id("ctl00_ContentPlaceHolder1_upProgress")).GetCssValue("display");

        if (elementImageLoading != "none")
        {
            await Task.Delay(1000);
            await WaitForLoading();
        }
    }
    catch (Exception)
    {
        Console.WriteLine("Error, image loading present.");
        driver?.Navigate().Refresh();
        await Task.Delay(1000);
    }
}

//ctl00_ContentPlaceHolder1_upProgress
//stykle dislay none
async Task CheckStatus()
{
    var fullCaseNumber = FullCaseNumber();

    await Task.Delay(500);

    await ClearnAndSendKeys("Visa_Case_Number", fullCaseNumber);

    var captcha64 = DownloadCaptcha();

    if (string.IsNullOrEmpty(captcha64))
    {
        return;
    }

    var captchaCode = await ReadCaptchaCode(captcha64);

    if (string.IsNullOrEmpty(captchaCode))
    {
        return;
    }

    await ClearnAndSendKeys("Captcha", captchaCode);

    try
    {
        driver.FindElement(By.Id("ctl00_ContentPlaceHolder1_btnSubmit")).Click();
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        Console.ReadLine();
    }
    
    await Task.Delay(800);

    await WaitForLoading();

    var lblError = ElementText("ctl00_ContentPlaceHolder1_lblError");
    var lblStatus = ElementText("ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblStatus");
    var lblDescription = ElementText("ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblMessage")?.Replace("\n", " - ")?.Replace("\r", " - ");
    var lblSubmitDate = ElementText("ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblSubmitDate");
    var lblStatusDate = ElementText("ctl00_ContentPlaceHolder1_ucApplicationStatusView_lblStatusDate");

    try
    {
        driver.FindElement(By.Id("ctl00_ContentPlaceHolder1_ucApplicationStatusView_lnkCloseInbox")).Click();
    }
    catch (Exception e)
    {
        Console.WriteLine("Not results popup found.");
        Console.WriteLine(e.Message);
    }

    var caseEstimatedDate = new DateTime(2020, 1, 1).AddDays(caseNumberDate - 501).ToString("yyyy-MM-dd");

    var logLine = $"{fullCaseNumber}|{lblStatus}|{caseEstimatedDate}|{lblSubmitDate}|{lblStatusDate}|{lblError}|{lblDescription}";
    SaveLog(logLine);
}

void SaveLog(string logLine)
{
    logLine += "\n";
    File.AppendAllText($"log{currentRunTime}.log ", logLine);
}

string? ElementText(string elementId)
{
    try
    {
        var elementText = driver?.FindElement(By.Id(elementId))?.Text;

        return elementText;
    }
    catch (Exception)
    {
        return "";
    }
}

async Task ClearnAndSendKeys(string id, string keys)
{
    driver.FindElement(By.Id(id)).Clear();
    await Task.Delay(800);
    driver.FindElement(By.Id(id)).SendKeys(keys);
}

string DownloadCaptcha()
{
    try
    {
        var captchaElementLocation = driver.FindElement(By.Id("c_status_ctl00_contentplaceholder1_defaultcaptcha_CaptchaImage")).Location;
        var captchaElementSize = driver.FindElement(By.Id("c_status_ctl00_contentplaceholder1_defaultcaptcha_CaptchaImage")).Size;

        var screenshot = ((ITakesScreenshot)driver).GetScreenshot();

        var ms = new MemoryStream();

        using var img = Image.FromStream(new MemoryStream(screenshot.AsByteArray)) as Bitmap;
        img?.Clone(new Rectangle(captchaElementLocation, captchaElementSize), img.PixelFormat).Save(ms, ImageFormat.Jpeg);

        var base64 = Convert.ToBase64String(ms.ToArray());

        return base64;
    }
    catch (Exception)
    {
        Console.WriteLine("Error while downloading captcha.");
        return "";
    }
}

async Task<string> ReadCaptchaCode(string base64)
{
    var captcha = new AntiCaptcha(captchaKey);
    var imageCaptcha = await captcha.SolveImage(base64);

    if (imageCaptcha.Success)
    {
        return imageCaptcha.Response;
    } 
    else
    {
        if (imageCaptcha.Response == "ERROR_NO_SLOT_AVAILABLE")
        {
            await Task.Delay(1000);
            return await ReadCaptchaCode(base64);
        }

        Console.WriteLine("Error from anti-captcha.");
        Console.WriteLine(imageCaptcha.Response);
        return "";
    }
}

void ReadIni()
{
    try
    {
        var iniLines = File.ReadAllLines("nvcstatus.ini");

        var pattern = @"(\w+)(\s|\t)*(\w+)";

        foreach (var iniLine in iniLines)
        {
            var match = new Regex(pattern).Match(iniLine);

            if (match.Success)
            {
                if (match.Groups[1].Value == "fromCaseNumberDate")
                {
                    fromCaseNumberDate = int.Parse(match.Groups[3].Value);
                }
                if (match.Groups[1].Value == "toCaseNumberDate")
                {
                    toCaseNumberDate = int.Parse(match.Groups[3].Value);
                }
                if (match.Groups[1].Value == "captchaKey")
                {
                    captchaKey = match.Groups[3].Value;
                }
            }
        }
    }
    catch (Exception)
    {
        Console.WriteLine($"Error while reading ini file.");
    }
}

void LoadState()
{
    try
    {
        var state = File.ReadAllText("state.txt");
        var stateInfo = state.Split(",");
        caseNumberDate = int.Parse(stateInfo[0]);
        caseNumberIndex = int.Parse(stateInfo[1]);
    }
    catch (Exception)
    {
        Console.WriteLine($"Error while reading {fileName}.");
        Console.WriteLine($"Proceeding with default values.");

        caseNumberDate = fromCaseNumberDate;
        caseNumberIndex = 1;
    }

    Console.WriteLine($"caseNumberPre: {caseNumberPre}");
    Console.WriteLine($"caseNumberDate: {caseNumberDate}");
    Console.WriteLine($"caseNumberIndex: {caseNumberIndex}");
}

void SaveState()
{
    File.WriteAllText(fileName, $"{caseNumberDate},{caseNumberIndex}");
}

string FullCaseNumber()
{
    var caseNumberFormatted = caseNumberIndex.ToString("D3");
    var fullCaseNumber = $"{caseNumberPre}{caseNumberDate}{caseNumberFormatted}";

    return fullCaseNumber;
}