using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

/// <summary>Tests that change process environment variables do not run in parallel with each other.</summary>
[CollectionDefinition("Environment variables", DisableParallelization = true)]
public sealed class EnvironmentCollection;
