using BotReport2026.Writers;

var rows = new List<string[]>
{
    new[] { "รหัสสถาบัน", "งวดข้อมูล", "เลขที่อ้างอิง", "คำอธิบายพฤติกรรม" },
    new[] { "0001", "2026-05", "1234567890", "โอนเงินผิดปกติ" },
    // edge cases: empty, embedded pipe, embedded quote, comma, newline
    new[] { "", "a|b", "say \"hi\"", "addr, line\r\nsecond" },
};
var path = Path.Combine(AppContext.BaseDirectory, "sample.csv");
CsvFile.Write(rows, path);
var bytes = File.ReadAllBytes(path);
Console.WriteLine("first 3 bytes (BOM): " + string.Join(" ", bytes.Take(3).Select(b => b.ToString("X2"))));
Console.WriteLine("--- file content ---");
Console.Write(File.ReadAllText(path));
Console.WriteLine("--- end ---");
