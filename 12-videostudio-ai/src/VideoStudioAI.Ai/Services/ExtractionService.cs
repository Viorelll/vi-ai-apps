using System.Diagnostics;
using System.Text;
using VideoStudioAI.Ai.Interfaces;
using VideoStudioAI.Ai.Models;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Domain.Models;

namespace VideoStudioAI.Ai.Services;

public sealed class ExtractionService(IProvider provider)
{
    public async Task<ExtractResult> ExtractAsync(ExtractRequest input, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.Base64);
        }
        catch (FormatException)
        {
            throw new GatewayException(400, "invalid_file", "Invalid base64 input.");
        }
        if (bytes.LongLength > VideoRules.MaxBytes)
            throw new GatewayException(413, "file_too_large", "File exceeds 50 MiB.");
        if (input.ContentType == "text/plain")
            return new(new UTF8Encoding(false, true).GetString(bytes));
        if (input.ContentType is "image/png" or "image/jpeg")
            return new(await provider.DescribeImageAsync(input.ContentType, input.Base64, ct));
        if (input.ContentType != "application/pdf")
            throw new GatewayException(
                409,
                "media_understanding_disabled",
                "Video understanding is an optional Content Understanding integration; the core generation pipeline does not require it."
            );
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, bytes, ct);
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo("pdftotext")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };
            p.StartInfo.ArgumentList.Add("-layout");
            p.StartInfo.ArgumentList.Add(path);
            p.StartInfo.ArgumentList.Add("-");
            p.Start();
            var output = p.StandardOutput.ReadToEndAsync(ct);
            var error = p.StandardError.ReadToEndAsync(ct);
            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch
            {
                p.Kill(true);
                throw;
            }
            var text = await output;
            await error;
            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(text))
                throw new GatewayException(
                    422,
                    "pdf_text_unavailable",
                    "PDF contains no extractable text. OCR requires a separately configured extraction provider."
                );
            return new(text);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
