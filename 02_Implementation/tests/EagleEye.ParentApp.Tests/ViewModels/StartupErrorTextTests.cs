using EagleEye.ParentApp.Core;
using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class StartupErrorTextTests
{
    public static TheoryData<Exception> StorageFailures => new()
    {
        new SqliteException("locked", 5),
        new InvalidDataException(),
        new IOException(),
        new UnauthorizedAccessException(),
    };

    [Fact]
    public void For_SecretStoreUnavailable_ReturnsKeychainText()
    {
        Assert.Equal(AppTexts.ErrorKeychainAccess, StartupErrorText.For(new SecretStoreUnavailableException("read", -25293)));
    }

    [Theory]
    [MemberData(nameof(StorageFailures))]
    public void For_StorageFailure_ReturnsStartupText(Exception exception)
    {
        Assert.Equal(AppTexts.ErrorStartup, StartupErrorText.For(exception));
    }

    [Fact]
    public void For_OtherException_ReturnsNull()
    {
        Assert.Null(StartupErrorText.For(new InvalidOperationException()));
    }

    [Fact]
    public void For_SecretStoreUnavailable_German_IsKeychainMessage()
    {
        var text = TestSupport.InCulture("de-DE", () => StartupErrorText.For(new SecretStoreUnavailableException("read", -1)));

        Assert.StartsWith("Die Kopplung konnte nicht aus dem macOS-Schlüsselbund gelesen werden.", text);
    }
}
