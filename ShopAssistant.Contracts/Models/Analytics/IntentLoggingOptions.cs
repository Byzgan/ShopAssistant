using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Models.Analytics;
public class IntentLoggingOptions
{
    public IntentLoggingMode Mode { get; set; } = IntentLoggingMode.All;
}