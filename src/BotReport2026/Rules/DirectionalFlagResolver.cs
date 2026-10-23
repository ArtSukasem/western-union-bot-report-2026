using BotReport2026.Models;

namespace BotReport2026.Rules;

public static class DirectionalFlagResolver
{
    // Both breach -> merged (suffix ""), one breaches -> that direction only, neither -> null.
    public static (List<RspTransaction> txns, string ruleCodeSuffix)? Resolve(
        bool ibBreach, List<RspTransaction> ibTxns,
        bool obBreach, List<RspTransaction> obTxns)
    {
        if (ibBreach && obBreach) return (ibTxns.Concat(obTxns).ToList(), "");
        if (ibBreach) return (ibTxns, "-IB");
        if (obBreach) return (obTxns, "-OB");
        return null;
    }
}
