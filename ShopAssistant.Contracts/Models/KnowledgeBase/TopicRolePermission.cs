namespace ShopAssistant.Contracts.Models.KnowledgeBase;

public class TopicRolePermission
{
    public string Topic { get; set; } = null!;
    public List<string> AllowedRoles { get; set; } = [];
}
