namespace EagleEye.ParentApp.Core.Communication;

/// <summary>A hub call was attempted while there is no confirmed paired connection.</summary>
public sealed class ParentHubNotConnectedException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    public ParentHubNotConnectedException()
        : base("The parent app is not connected to the service.")
    {
    }
}
