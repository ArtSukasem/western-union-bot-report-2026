using OfficeOpenXml;

namespace BotReport2026;

static class Program
{
    [STAThread]
    static void Main()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }
}