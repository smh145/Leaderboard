### LeaderboardService
Thank you to all the teachers for your guidance and patient waiting, giving me the opportunity to complete such a program. I'm really happy ^_^

Compared to the previous version, this version has several major improvements:

1.  **Bucketing strategy**: Although the previous version considered score-based bucketing, it used a global lock and small buckets (100 points / bucket), leading to many cross-bucket operations and poor prefix sum maintenance with O(n) complexity. This version considers lock granularity by dividing into 32 buckets with manually specified boundaries. These can be adjusted based on business hotspots - hotspot segments can have smaller buckets, and bucket sizes should ideally exceed 2000 to reduce the probability of cross-bucket operations.

2.  **Data structure optimization**: A significant portion of the development time for the previous version was spent attempting to implement custom locks and thread queues, but these efforts ultimately failed, wasting most of the time. Using `SortedSet` resulted in O(n) time complexity for rank-based positioning and querying customer rankings within buckets. This version implements `SkipList`, optimizing both operations to O(log n) complexity. In the current business scenario, the skip list is unlikely to degrade to its worst-case performance.

3.  **Code complexity**: The code may seem verbose, but this is intentional to achieve finer-grained locking.
