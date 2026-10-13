using EvalAI.Domain.DTOs;
using EvalAI.Domain.Models;
using Microsoft.Extensions.Configuration;
namespace EvalAI.Services.Evaluations;

public sealed class VariantCatalog(IConfiguration config)
{
    public static readonly Guid A = Guid.Parse("00000000-0000-4000-8000-00000000000a");
    public static readonly Guid B = Guid.Parse("00000000-0000-4000-8000-00000000000b");
    public VariantDefinition[] All => [
     new(A,"A",config["Foundry:CandidateA"]??"evalai-a",config["Foundry:CandidateA:PromptVersion"]??"candidate-v1",config["Foundry:CandidateA:ToolsetVersion"]??"none-v1"),
  new(B,"B",config["Foundry:CandidateB"]??"evalai-b",config["Foundry:CandidateB:PromptVersion"]??"candidate-v1",config["Foundry:CandidateB:ToolsetVersion"]??"none-v1")
    ];
    public VariantDefinition[] Resolve(Guid[] ids)
    {
        if (ids.Length is < 1 or > 2 || ids.Distinct().Count() != ids.Length) throw new AppException(400, "invalid_variants", "Select one or two distinct configured variants.");
        return ids.Select(id => All.SingleOrDefault(x => x.Id == id) ?? throw new AppException(400, "variant_not_allowed", "Unknown variant.")).ToArray();
    }
    public string JudgeDeployment => config["Foundry:JudgeDeployment"] ?? "evalai-judge";
    public string Mode => config["Foundry:Mode"] ?? "Live";
}
