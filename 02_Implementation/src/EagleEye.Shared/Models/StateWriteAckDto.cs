namespace EagleEye.Shared.Models;

/// <summary>Acknowledgement of an accepted write (ADR-010 §5). Shared by all state areas.</summary>
/// <param name="Revision">The revision the write produced.</param>
public sealed record StateWriteAckDto(long Revision);
