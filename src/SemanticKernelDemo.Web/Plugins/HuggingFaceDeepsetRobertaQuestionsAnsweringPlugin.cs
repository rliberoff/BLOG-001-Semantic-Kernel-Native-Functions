using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.SkillDefinition;

using SemanticKernelDemo.Web.Options;

namespace SemanticKernelDemo.Web.Plugins;

/// <summary>
/// Provides functions to answer questions from a given context using a <see href="https://arxiv.org/abs/1907.11692"><c>RoBERTa</c></see> base model 
/// created by <see href="https://www.deepset.ai/"><c>deepset</c></see> and hosted in <see href="https://huggingface.co/deepset/roberta-base-squad2">Hugging Face</see>.
/// </summary>
/// <see href="https://huggingface.co/deepset/roberta-base-squad2"/>
internal sealed class HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin
{
    public const string SkillName = nameof(HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin);

    public const string AskQuestionWithContextFunction = @"AskQuestionWithContext";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly HuggingFaceOptions huggingFaceOptions;


    public HuggingFaceDeepsetRobertaQuestionsAnsweringPlugin(IHttpClientFactory httpClientFactory, IOptions<HuggingFaceOptions> huggingFaceOptions)
    {
        this.httpClientFactory = httpClientFactory;
        this.huggingFaceOptions = huggingFaceOptions.Value;
    }

    [SKFunction, Description("Answer questions from a given context.")]
    public async Task<string> AskQuestionWithContextAsync(
        [Description("The context with the information from which questions can be answered.")] string context,
        [Description("The question to answer with information from the context.")] string question,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(@"Bearer", huggingFaceOptions.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, @"https://api-inference.huggingface.co/models/deepset/roberta-base-squad2")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                inputs = new
                {
                    question,
                    context,
                },
            }), Encoding.UTF8, @"application/json"),
        };

        using var response = (await httpClient.SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
