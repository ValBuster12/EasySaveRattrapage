using EasySave.Protocol.Messages;

namespace EasySave.Server;

/// <summary>
///     Centralizes broker routing predicates so they are reusable and testable.
/// </summary>
internal static class BrokerRoutingPolicy
{
    /// <summary>
    ///     Returns <c>true</c> when a host session is allowed to publish host telemetry.
    /// </summary>
    public static bool CanPublishHostMessage(ProtocolClientKind? clientKind, string? instanceId)
    {
        return clientKind == ProtocolClientKind.EasySaveHost && !string.IsNullOrWhiteSpace(instanceId);
    }

    /// <summary>
    ///     Returns <c>true</c> when a remote session is actively subscribed to a host instance.
    /// </summary>
    public static bool IsSubscribedRemote(ProtocolClientKind? clientKind, string? subscribedInstanceId, string hostInstanceId)
    {
        return clientKind == ProtocolClientKind.RemoteConsole
               && !string.IsNullOrWhiteSpace(subscribedInstanceId)
               && string.Equals(subscribedInstanceId, hostInstanceId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Returns <c>true</c> when remote -> host command relay is allowed.
    /// </summary>
    public static bool CanRelayRemoteCommand(ProtocolClientKind? clientKind, CommandRequestMessage? request)
    {
        return clientKind == ProtocolClientKind.RemoteConsole
               && request is not null
               && !string.IsNullOrWhiteSpace(request.TargetInstanceId);
    }
}
