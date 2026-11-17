using Xunit;

namespace Zaplio.Tests;

/// <summary>
/// Tests that build WPF objects on their own STA threads run one after another. The settings window test creates the
/// Application and loads the theme on its thread; theme brushes are dynamic resources, so controls built at the same time
/// on another thread would resolve them across threads and could lock up the test run.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfCollection
{
    public const string Name = "WPF";
}
