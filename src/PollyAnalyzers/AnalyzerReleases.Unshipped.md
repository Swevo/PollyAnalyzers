### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PLY001  | Reliability | Warning | Blocking call on async API may cause a deadlock (.Result/.Wait()/.GetAwaiter().GetResult())
PLY002  | Reliability | Warning | Avoid 'async void' methods (except event handlers)
PLY003  | Reliability | Warning | Fire-and-forget task is not awaited, assigned, or discarded
PLY004  | Reliability | Warning | Empty catch block swallows exceptions
PLY005  | Reliability | Info    | Async call drops an available CancellationToken
