using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAssistant.Infrastructure.KnowledgeBase;
using ShopAssistant.Infrastructure.Validations;

namespace ShopAssistant.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(KnowledgeExporter knowledgeExporter, IntentKnowledgeValidationService validationService) : ControllerBase
{
    [HttpGet("export-knowledge-base")]
    public async Task<IActionResult> ExportKnowledgeBase()
    {
        await knowledgeExporter.ExportAsync();
        return Ok(new { success = true, message = "The knowledge base has been exported successfully." });
    }


    [HttpGet("intent-knowledge-conflicts")]
    public async Task<IActionResult> ValidateIntentKnowledgeConflicts()
    {
        var conflicts = await validationService.ValidateIntentKnowledgeConflictsAsync();

        if (!conflicts.Any())
            return Ok(new { success = true, message = "No conflicts found." });

        var report = conflicts.Select(conflict => new
        {
            conflict.Language,
            KnowledgeId = conflict.KnowledgeItem.Id,
            KnowledgeTopic = conflict.KnowledgeItem.Topic.ToString(),
            conflict.Question,
            Intent = conflict.IntentPattern.Intent.ToString(),
            MatchType = conflict.MatchType.ToString(),
            conflict.Score,
            conflict.MatchedPhrase
        }).ToList();

        return Conflict(new
        {
            success = false,
            message = "Conflicts detected between intent patterns and knowledge base questions.",
            conflicts = report
        });
    }
}