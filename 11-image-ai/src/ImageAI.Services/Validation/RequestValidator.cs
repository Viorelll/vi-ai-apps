using ImageAI.Domain.Models;

namespace ImageAI.Services.Validation;

public static class RequestValidator
{
    public static string Brief(string? value)
    {
        var result = value?.Trim() ?? "";
        if (result.Length is < 5 or > 4000)
            throw new DomainException("brief_length_5_to_4000");
        return result;
    }

    public static void Parameters(string size, string quality)
    {
        if (size is not ("1024x1024" or "1536x1024" or "1024x1536"))
            throw new DomainException("unsupported_size");
        if (quality is not ("low" or "medium" or "high"))
            throw new DomainException("unsupported_quality");
    }

    public static void RequestId(Guid id)
    {
        if (id == Guid.Empty)
            throw new DomainException("request_id_required");
    }
}
