using System.ComponentModel.DataAnnotations;

namespace SemanticKernelDemo.Web.Controllers.Models;

public class AskRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Context { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string Question { get; init; }
}
