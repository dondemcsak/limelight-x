using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;
using Xunit;

namespace LimelightX.UI.Tests.Services;

public class LogServiceTests
{
    [Fact]
    public void Log_AddsEntry()
    {
        var service = new LogService();
        var error = new UiError { Code = "ERR_TEST", Message = "test", Severity = ErrorSeverity.Error, Category = ErrorCategory.Validation };

        service.Log(error);

        Assert.Single(service.Entries);
        Assert.Equal("ERR_TEST", service.Entries[0].Code);
    }

    [Fact]
    public void Log_ExceedingCapacity_DropsOldestEntries()
    {
        var service = new LogService(capacity: 3);

        for (var i = 0; i < 5; i++)
        {
            service.Log(new UiError { Code = $"ERR_{i}", Message = "test", Severity = ErrorSeverity.Info, Category = ErrorCategory.State });
        }

        Assert.Equal(3, service.Entries.Count);
        Assert.Equal("ERR_2", service.Entries[0].Code);
        Assert.Equal("ERR_4", service.Entries[2].Code);
    }
}
