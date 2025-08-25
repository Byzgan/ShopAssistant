using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopAssistant.Contracts.Interfaces.Chat;
using ShopAssistant.Contracts.Models.Chat;

namespace ShopAssistant.Api.Controllers;

/// <summary>
/// Controller for chat interactions with the AI assistant.
/// Handles user questions and returns generated responses.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController(IChatService chatService, IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Processes a user message to the assistant, including ambiguity handling and clarification.
    /// </summary>
    /// <param name="request">Chat request from the user, including message, optional language, and optional UserClarification.</param>
    /// <returns>ChatResponse containing the answer or an ambiguity prompt.</returns>
    [HttpPost("message")]
    public async Task<IActionResult> AskAsync([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        if (string.IsNullOrWhiteSpace(request.Language))
            request.Language = configuration.GetValue<string>("Languages:Default") ?? "en";

        ChatResponse? answer = await chatService.ProcessMessageAsync(request);

        return Ok(answer);
    }
}
