using EasySave.Models.Network;
using EasySave.Protocol.Messages;

namespace EasySaveTest;

public class RemoteCommandDispatcherTests
{
    [Test]
    public async Task DispatchAsync_PauseCommand_InvokesPauseAction()
    {
        var pauseCalls = 0;
        var resumeCalls = 0;
        var stopCalls = 0;
        var startCalls = 0;

        var handled = await RemoteCommandDispatcher.DispatchAsync(
            CommandType.Pause,
            pause: () => { pauseCalls++; return Task.CompletedTask; },
            resume: () => { resumeCalls++; return Task.CompletedTask; },
            stop: () => { stopCalls++; return Task.CompletedTask; },
            startAsync: () => { startCalls++; return Task.CompletedTask; });

        Assert.That(handled, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(pauseCalls, Is.EqualTo(1));
            Assert.That(resumeCalls, Is.EqualTo(0));
            Assert.That(stopCalls, Is.EqualTo(0));
            Assert.That(startCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task DispatchAsync_StartCommand_InvokesStartAction()
    {
        var startCalls = 0;
        var handled = await RemoteCommandDispatcher.DispatchAsync(
            CommandType.Start,
            pause: () => Task.CompletedTask,
            resume: () => Task.CompletedTask,
            stop: () => Task.CompletedTask,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            });

        Assert.That(handled, Is.True);
        Assert.That(startCalls, Is.EqualTo(1));
    }
}
