I've implemented two different leaderboard services, but neither can handle millions QPS， only handle hundreds thousand QPS.

### LeaderboardService
Leaderboard use ReaderWriterLockSlim
ReaderWriterLockSlim does not strictly guarantee FIFO on the server side
So the Leaderboard is not completely real-time
If a fair reader-writer lock could be implemented, further optimizations are possible (I failed):
1. For an update request, the current score can be returned immediately, while the Leaderboard update operation waits for the lock.
2. Consecutive write requests can be merged. Updates for the same customerId can be combined, reducing the number of write lock acquisitions.
3. Rebuild the cache when switching between read and write locks.

### SnapshotLeaderboardService
Snapshot leaderboard implementation using background task and buffering.
This leaderboard service achieves more efficient score updates and rank queries by buffering score changes in a queue 
and periodically processing these updates via a background task to build leaderboard snapshots.
1. When updating scores, the service adds update requests to the queue and immediately returns the latest score to the caller.
2. Reduces write lock hold time, improving concurrent processing capability.
3. During periodic leaderboard rebuilds, merges score change operations for the same user, reducing the number of sorts.
4. Reduces the frequency of cache rebuilds.
However, this may result in situations where score updates succeed but rank queries do not yet reflect the latest scores.
