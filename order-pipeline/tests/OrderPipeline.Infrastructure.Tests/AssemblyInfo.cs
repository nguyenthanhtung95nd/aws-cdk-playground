using Xunit;

// CDK's .NET bindings drive a single Node child process. Running these classes in parallel
// makes concurrent calls into it and the process dies mid-run.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
