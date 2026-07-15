using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LimelightX.UI.Tests.Services;

public class AppLoggingTests
{
    private const string LogFileName = "Limelight-x-log.txt";

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "LimelightXTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void CreateLoggerFactory_EmptyLogPath_WritesToConfigFileDirectory()
    {
        var configDir = CreateTempDirectory();
        var configFilePath = Path.Combine(configDir, "config.json");

        using (var factory = AppLogging.CreateLoggerFactory(logPath: null, configFilePath))
        {
            factory.CreateLogger("Test").LogError("boom");
        }

        var logFile = Path.Combine(configDir, LogFileName);
        Assert.True(File.Exists(logFile));
        Assert.Contains("boom", File.ReadAllText(logFile));
    }

    [Fact]
    public void CreateLoggerFactory_CustomLogPath_WritesThereInsteadOfDefault()
    {
        var configDir = CreateTempDirectory();
        var configFilePath = Path.Combine(configDir, "config.json");
        var customDir = CreateTempDirectory();

        using (var factory = AppLogging.CreateLoggerFactory(customDir, configFilePath))
        {
            factory.CreateLogger("Test").LogError("boom");
        }

        Assert.False(File.Exists(Path.Combine(configDir, LogFileName)));
        Assert.True(File.Exists(Path.Combine(customDir, LogFileName)));
    }

    [Fact]
    public void CreateLoggerFactory_CalledAgainAtSameLocation_AppendsAcrossSessions()
    {
        var dir = CreateTempDirectory();
        var configFilePath = Path.Combine(dir, "config.json");

        using (var factory = AppLogging.CreateLoggerFactory(null, configFilePath))
        {
            factory.CreateLogger("Test").LogError("first session");
        }

        using (var factory = AppLogging.CreateLoggerFactory(null, configFilePath))
        {
            factory.CreateLogger("Test").LogError("second session");
        }

        var contents = File.ReadAllText(Path.Combine(dir, LogFileName));
        Assert.Contains("first session", contents);
        Assert.Contains("second session", contents);
    }

    [Fact]
    public void CreateLoggerFactory_RedirectsToNewLocation_OldLocationGetsNoFurtherEntries()
    {
        var configFilePath = Path.Combine(CreateTempDirectory(), "config.json");
        var locationA = CreateTempDirectory();
        var locationB = CreateTempDirectory();

        using (var factoryA = AppLogging.CreateLoggerFactory(locationA, configFilePath))
        {
            factoryA.CreateLogger("Test").LogError("entry at A");
        }

        using (var factoryB = AppLogging.CreateLoggerFactory(locationB, configFilePath))
        {
            factoryB.CreateLogger("Test").LogError("entry at B");
        }

        var contentsA = File.ReadAllText(Path.Combine(locationA, LogFileName));
        var contentsB = File.ReadAllText(Path.Combine(locationB, LogFileName));
        Assert.Contains("entry at A", contentsA);
        Assert.DoesNotContain("entry at B", contentsA);
        Assert.Contains("entry at B", contentsB);
    }

    [Theory]
    [InlineData(ErrorSeverity.Info, "Information")]
    [InlineData(ErrorSeverity.Warning, "Warning")]
    [InlineData(ErrorSeverity.Error, "Error")]
    [InlineData(ErrorSeverity.Fatal, "Critical")]
    public void LogUiError_MapsSeverityToDocumentedLogLevel(ErrorSeverity severity, string expectedLevelName)
    {
        var dir = CreateTempDirectory();
        var configFilePath = Path.Combine(dir, "config.json");
        var error = new UiError
        {
            Code = "ERR_TEST",
            Message = "something went wrong",
            Severity = severity,
            Category = ErrorCategory.Pipeline,
        };

        using (var factory = AppLogging.CreateLoggerFactory(null, configFilePath))
        {
            AppLogging.LogUiError(factory.CreateLogger("Test"), error);
        }

        var line = File.ReadAllText(Path.Combine(dir, LogFileName));
        Assert.Contains($"[{expectedLevelName}]", line);
        Assert.Contains("ERR_TEST: something went wrong (Category=Pipeline)", line);
    }

    [Fact]
    public void LogUiError_WithLocation_AppendsLineAndColumn()
    {
        var dir = CreateTempDirectory();
        var configFilePath = Path.Combine(dir, "config.json");
        var error = new UiError
        {
            Code = "ERR_CNL_PARSE",
            Message = "Missing period.",
            Severity = ErrorSeverity.Error,
            Category = ErrorCategory.Pipeline,
            Location = new ErrorLocation { Line = 3, Column = 14 },
        };

        using (var factory = AppLogging.CreateLoggerFactory(null, configFilePath))
        {
            AppLogging.LogUiError(factory.CreateLogger("Test"), error);
        }

        var line = File.ReadAllText(Path.Combine(dir, LogFileName));
        Assert.Contains("Missing period. (line 3, column 14) (Category=Pipeline)", line);
    }

    [Fact]
    public void CreateLoggerFactory_UnwritableDirectory_DoesNotThrowAndReturnsNoOpLogger()
    {
        // A path through an existing *file* can never be created as a directory.
        var blockingFile = Path.Combine(CreateTempDirectory(), "not-a-directory.txt");
        File.WriteAllText(blockingFile, "blocking");
        var invalidLogPath = Path.Combine(blockingFile, "logs");
        var configFilePath = Path.Combine(CreateTempDirectory(), "config.json");

        var factory = AppLogging.CreateLoggerFactory(invalidLogPath, configFilePath);
        var logger = factory.CreateLogger("Test");

        var exception = Record.Exception(() => logger.LogError("this must not throw"));
        Assert.Null(exception);
    }
}
