using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Tests.Fakes;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class ConnectionMessageTextTests
{
    [Theory]
    [InlineData(ConnectionMessage.InvalidHost, "Please enter a valid hostname or IP address.")]
    [InlineData(ConnectionMessage.Unreachable, "Connection error: could not connect to the EagleEye service at kid-pc.")]
    [InlineData(ConnectionMessage.CodeFormat, "The pairing code has 6 digits.")]
    [InlineData(ConnectionMessage.WrongCode, "The pairing code is wrong.")]
    [InlineData(ConnectionMessage.CodeExpired, "The pairing code has expired.")]
    [InlineData(ConnectionMessage.NoPendingCode, "No valid pairing code. Please request a new code.")]
    [InlineData(ConnectionMessage.DeviceNameRequired, "Please enter a device name (up to 50 characters).")]
    [InlineData(ConnectionMessage.CertificateChanged, "The identity of the EagleEye PC kid-pc has changed. The connection was refused.")]
    [InlineData(ConnectionMessage.PairingLost, "This PC is no longer paired with kid-pc.")]
    [InlineData(ConnectionMessage.RemoveFailed, "The pairing could not be removed. Please try again.")]
    public void Compose_English(ConnectionMessage message, string expected)
    {
        var text = TestSupport.InCulture("en-US", () => ConnectionMessageText.Compose(State(message)));

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(ConnectionMessage.Unreachable, "Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter kid-pc möglich.")]
    [InlineData(ConnectionMessage.WrongCode, "Der Kopplungscode ist falsch.")]
    [InlineData(ConnectionMessage.CodeExpired, "Der Kopplungscode ist abgelaufen.")]
    [InlineData(ConnectionMessage.DeviceNameRequired, "Bitte einen Gerätenamen eingeben (höchstens 50 Zeichen).")]
    public void Compose_German(ConnectionMessage message, string expected)
    {
        var text = TestSupport.InCulture("de-DE", () => ConnectionMessageText.Compose(State(message)));

        Assert.Equal(expected, text);
    }

    [Fact]
    public void Compose_None_ReturnsNull()
    {
        Assert.Null(ConnectionMessageText.Compose(State(ConnectionMessage.None)));
    }

    private static ConnectionState State(ConnectionMessage message) => ConnectionState.Initial with { Host = "kid-pc", LastMessage = message };
}
