using EagleEye.Service.SessionAgent;
using EagleEye.Service.UserAccounts;
using Moq;
using Xunit;

namespace EagleEye.Service.Tests;

/// <summary>Tests that change process environment variables do not run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentCollection
{
    public const string Name = "Environment variables";
}
