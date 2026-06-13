namespace BotReport2026.Models;

public class SbeRecord
{
    public string ReportingPeriod { get; set; } = "";       // YYYY-MM
    public string ReferenceNo { get; set; } = "";           // SBE{YYYYMM}{seq:D4}
    public string PersonType { get; set; } = "บุคคลธรรมดา";
    public string FlagPersonType { get; set; } = "1";       // 0=นิติบุคคล, 1=บุคคลธรรมดา
    public string Title { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PersonRefId { get; set; } = "";
    public decimal? MonthlyIncome { get; set; }
    public string AccountDetail { get; set; } = "";
    public string FlagSavingsAccount { get; set; } = "1";
    public string FlagEMoney { get; set; } = "0";
    public DateTime? AnomalyDate { get; set; }
    public string BehaviorType { get; set; } = "";          // rule number
    public string MtcnList { get; set; } = "";
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string RuleCode { get; set; } = "";
    public bool HasSae { get; set; }
}
