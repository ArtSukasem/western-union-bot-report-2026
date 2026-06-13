namespace BotReport2026.Models;

public class SaeRecord
{
    public string ReferenceNo { get; set; } = "";
    public string ReportingPeriod { get; set; } = "";
    public string PersonType { get; set; } = "บุคคลธรรมดา";
    public DateTime? EddCompletionDate { get; set; }
    public string EddResult { get; set; } = "ผ่าน EDD";
    public string PersonDetail { get; set; } = "";
}
