namespace VideoStudioAI.Services.Interfaces;

public interface IMediaValidator
{
    Task ValidateAsync(string path, int seconds, int width, int height, CancellationToken ct);
}
