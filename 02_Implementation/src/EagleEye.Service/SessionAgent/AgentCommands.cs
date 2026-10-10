namespace EagleEye.Service.SessionAgent;

/// <summary>A process the agent is asked to close: PID plus creation time (FILETIME ticks), the process identity.</summary>
/// <param name="Pid">The process ID.</param>
/// <param name="Created">The creation time (FILETIME ticks, as <c>GetProcessTimes</c> returns it).</param>
public sealed record CloseTarget(int Pid, long Created);

/// <summary>
/// Service → agent command (ADR-013 §4, ADR-011 amendment): post <c>WM_CLOSE</c> to the app windows of the targets.
/// Wire form: <c>{"cmd":"close","id":17,"targets":[{"pid":4711,"created":133420000000000000}]}</c>.
/// </summary>
/// <param name="Id">Correlation id, echoed in the answer.</param>
/// <param name="Targets">1 to <see cref="AgentCommandReader.MaxTargets"/> processes.</param>
public sealed record CloseCommand(long Id, IReadOnlyList<CloseTarget> Targets);

/// <summary>
/// Agent → service answer to a <see cref="CloseCommand"/>: <c>{"closed":{"id":17,"windows":2,"missing":0}}</c>.
/// </summary>
/// <param name="Id">The command's id.</param>
/// <param name="Windows">Number of windows <c>WM_CLOSE</c> was posted to.</param>
/// <param name="Missing">Targets for which no window was posted to (gone, PID reused, no app window, post failed).</param>
public sealed record CloseAnswer(long Id, int Windows, int Missing);
