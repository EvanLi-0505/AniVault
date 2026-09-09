// The file-backed SQLite helpers (TestDatabase / TestLibrary) and BackupService.RestoreAsync
// clear connection pools process-wide, which is unsafe to run concurrently. The suite is
// fast, so run it sequentially.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
