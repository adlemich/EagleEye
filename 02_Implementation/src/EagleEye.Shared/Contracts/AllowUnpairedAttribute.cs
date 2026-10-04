namespace EagleEye.Shared.Contracts;

/// <summary>
/// Marks a parent hub method as callable by connections that are not paired (ADR-008 §4).
/// All other parent hub methods are denied to unpaired connections by default. The service
/// reads the attribute from the hub implementation method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowUnpairedAttribute : Attribute;
