using EagleEye.ParentApp.Core.Abstractions;
using Xunit;

namespace EagleEye.ParentApp.Tests.Abstractions;

public sealed class SecretStoreUnavailableExceptionTests
{
    [Fact]
    public void Constructor_SetsOperationAndStatus()
    {
        var exception = new SecretStoreUnavailableException("write", -25308);

        Assert.Equal(("write", -25308), (exception.Operation, exception.Status));
    }

    [Fact]
    public void Message_NamesOperationAndStatus()
    {
        var exception = new SecretStoreUnavailableException("read", -34018);

        Assert.Equal("The secret store refused the read operation (status -34018).", exception.Message);
    }

    [Fact]
    public void Type_IsInvalidOperationException()
    {
        Assert.IsAssignableFrom<InvalidOperationException>(new SecretStoreUnavailableException("delete", -1));
    }
}
