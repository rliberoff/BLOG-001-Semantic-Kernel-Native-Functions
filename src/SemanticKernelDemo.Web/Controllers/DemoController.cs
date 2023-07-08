using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;

using SemanticKernelDemo.Web.Controllers.Models;
using SemanticKernelDemo.Web.Plugins;

namespace SemanticKernelDemo.Web.Controllers;

/// <summary>
/// A demo controller.
/// </summary>
[ApiController]
[Produces(@"application/json")]
[Route(@"api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IKernel kernel;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoController"/> class.
    /// </summary>
    /// <param name="kernel">A valid instance of the <see cref="IKernel"/> from <c>Semantic Kernel</c>.</param>
    public DemoController(IKernel kernel)
    {
        this.kernel = kernel;
    }

    /// <summary>
    /// Answers a question from a context.
    /// </summary>
    /// <param name="request">The request with the context and the question to answer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> containing an <see cref="IActionResult"/> with the result of this operation.</returns>
    /// <response code="200">The answer for the question from the context.</response>
    /// <response code="400">If the request is invalid or poorly constructed.</response>
    /// <response code="404">If no answer could be found.</response>
    [HttpPost(@"ask")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AskResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))]
    public async Task<IActionResult> AskAsync([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        const double MinimumScore = 0.7;

        var variables = new ContextVariables();
        variables.Set(@"question", request.Question);
        variables.Set(@"context", request.Context);

        var skillName = HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin.SkillName;
        var skillFunctionName = HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin.AskQuestionWithContextFunction;

        var context = await kernel.RunAsync(variables, cancellationToken, kernel.Skills.GetFunction(skillName, skillFunctionName));

        if (context.ErrorOccurred)
        {
            return Problem(context.LastErrorDescription);
        }

        var response = JsonSerializer.Deserialize<AskResponse>(context.Result, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        return response.Score > MinimumScore
                ? Ok(response)
                : NotFound($@"No good answer found. Score was {response.Score}.");
    }
}
