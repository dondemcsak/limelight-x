using LimelightX.UI.Services;
using Xunit;

namespace LimelightX.UI.Tests.Settings;

/// <summary>
/// Exercises the real Windows Credential Manager (not mocked) using a
/// scratch credential name distinct from the production
/// "LimelightX/AnthropicApiKey" one, so this never touches a real user's
/// stored key.
/// </summary>
public class CredentialServiceTests
{
    private static string ScratchName() => $"LimelightX.Tests/{Guid.NewGuid()}";

    [Fact]
    public void ReadApiKey_NeverWritten_ReturnsNull()
    {
        var service = new CredentialService(ScratchName());

        Assert.Null(service.ReadApiKey());
    }

    [Fact]
    public void WriteThenRead_RoundTripsCorrectly()
    {
        var name = ScratchName();
        var service = new CredentialService(name);
        try
        {
            service.WriteApiKey("sk-ant-test-12345");

            Assert.Equal("sk-ant-test-12345", service.ReadApiKey());
        }
        finally
        {
            service.DeleteApiKey();
        }
    }

    [Fact]
    public void WriteTwice_OverwritesPreviousValue()
    {
        var name = ScratchName();
        var service = new CredentialService(name);
        try
        {
            service.WriteApiKey("first-value");
            service.WriteApiKey("second-value");

            Assert.Equal("second-value", service.ReadApiKey());
        }
        finally
        {
            service.DeleteApiKey();
        }
    }

    [Fact]
    public void DeleteApiKey_RemovesCredential()
    {
        var name = ScratchName();
        var service = new CredentialService(name);
        service.WriteApiKey("to-be-deleted");

        service.DeleteApiKey();

        Assert.Null(service.ReadApiKey());
    }

    [Fact]
    public void DeleteApiKey_NeverWritten_DoesNotThrow()
    {
        var service = new CredentialService(ScratchName());

        var exception = Record.Exception(() => service.DeleteApiKey());

        Assert.Null(exception);
    }
}
