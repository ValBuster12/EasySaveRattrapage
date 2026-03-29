using EasySave.Protocol.Messages;
using EasySave.Server;

namespace EasySaveTest;

public class BrokerRoutingPolicyTests
{
    [Test]
    public void CanPublishHostMessage_RequiresHostRoleAndInstanceId()
    {
        Assert.That(BrokerRoutingPolicy.CanPublishHostMessage(ProtocolClientKind.EasySaveHost, "host-1"), Is.True);
        Assert.That(BrokerRoutingPolicy.CanPublishHostMessage(ProtocolClientKind.RemoteConsole, "host-1"), Is.False);
        Assert.That(BrokerRoutingPolicy.CanPublishHostMessage(ProtocolClientKind.EasySaveHost, " "), Is.False);
    }

    [Test]
    public void IsSubscribedRemote_IsCaseInsensitiveForInstanceId()
    {
        Assert.That(BrokerRoutingPolicy.IsSubscribedRemote(ProtocolClientKind.RemoteConsole, "HOST-A", "host-a"), Is.True);
        Assert.That(BrokerRoutingPolicy.IsSubscribedRemote(ProtocolClientKind.RemoteConsole, "HOST-B", "host-a"), Is.False);
    }

    [Test]
    public void CanRelayRemoteCommand_RequiresRemoteRoleAndTargetHost()
    {
        var request = new CommandRequestMessage(
            "req-1",
            "host-1",
            1,
            "job",
            CommandType.Start,
            "console",
            DateTimeOffset.UtcNow);

        Assert.That(BrokerRoutingPolicy.CanRelayRemoteCommand(ProtocolClientKind.RemoteConsole, request), Is.True);
        Assert.That(BrokerRoutingPolicy.CanRelayRemoteCommand(ProtocolClientKind.EasySaveHost, request), Is.False);
        Assert.That(BrokerRoutingPolicy.CanRelayRemoteCommand(ProtocolClientKind.RemoteConsole, request with { TargetInstanceId = " " }), Is.False);
    }
}
