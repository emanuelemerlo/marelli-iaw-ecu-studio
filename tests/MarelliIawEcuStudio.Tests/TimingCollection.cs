namespace MarelliIawEcuStudio.Tests;

/// <summary>
/// Tests that depend on line timing (the 5-baud init, the network bridge,
/// the command line against the simulators) run one at a time, so a shared
/// CI machine busy with other tests does not delay their deadlines.
/// </summary>
[CollectionDefinition(nameof(TimingCollection), DisableParallelization = true)]
public class TimingCollection;
