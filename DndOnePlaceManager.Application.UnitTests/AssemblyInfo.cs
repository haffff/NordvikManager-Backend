using Xunit;

// PermissionsExtension.ServiceProvider is a process-wide static, set per-test-class in
// each handler test base constructor. Running test classes in parallel (xUnit's default)
// lets two classes stomp on that static concurrently, causing intermittent cross-class
// failures. Disable parallelization until PermissionsExtension is refactored away from
// the static service-locator pattern.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
