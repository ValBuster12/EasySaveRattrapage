using EasySave.Protocol.Messages;

namespace EasySave.Models.Network;

/// <summary>
///     Maps remote protocol commands to host actions.
/// </summary>
internal static class RemoteCommandDispatcher
{
    /// <summary>
    ///     Executes the action mapped to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">Remote command to execute.</param>
    /// <param name="pause">Pause action.</param>
    /// <param name="resume">Resume action.</param>
    /// <param name="stop">Stop action.</param>
    /// <param name="startAsync">Start action.</param>
    /// <returns><c>true</c> when command was mapped and executed; otherwise <c>false</c>.</returns>
    public static Task<bool> DispatchAsync(
        CommandType command,
        Func<Task> pause,
        Func<Task> resume,
        Func<Task> stop,
        Func<Task> startAsync)
    {
        return command switch
        {
            CommandType.Pause => ExecuteAsync(pause),
            CommandType.Resume => ExecuteAsync(resume),
            CommandType.Stop => ExecuteAsync(stop),
            CommandType.Start => ExecuteAsync(startAsync),
            _ => Task.FromResult(false)
        };
    }

    private static async Task<bool> ExecuteAsync(Func<Task> action)
    {
        await action();
        return true;
    }
}
