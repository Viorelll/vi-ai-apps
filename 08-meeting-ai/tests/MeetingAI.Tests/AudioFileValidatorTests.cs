using MeetingAI.Api.Services;
using Xunit;
namespace MeetingAI.Tests;

public sealed class AudioFileValidatorTests
{
    [Fact] public void DetectsWaveByContent() { var header = "RIFF0000WAVE"u8; Assert.Equal("audio/wav", AudioFileValidator.DetectContentType("recording.wav", header)); }
    [Fact] public void RejectsExtensionWithIncorrectMagicBytes() { Assert.Null(AudioFileValidator.DetectContentType("recording.wav", "not a wave file"u8)); }
    [Fact] public void DetectsMpegFrame() { byte[] header = [0xff, 0xfb, 0x90, 0x64]; Assert.Equal("audio/mpeg", AudioFileValidator.DetectContentType("recording.mp3", header)); }
    [Fact] public void DetectsOgg() { Assert.Equal("audio/ogg", AudioFileValidator.DetectContentType("recording.ogg", "OggS0012"u8)); }
    [Fact] public void RejectsUnsupportedExtension() { Assert.Null(AudioFileValidator.DetectContentType("document.pdf", "%PDF-1.7"u8)); }
}
