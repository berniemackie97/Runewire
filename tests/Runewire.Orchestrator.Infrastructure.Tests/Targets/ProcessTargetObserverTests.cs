using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Targets;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Tests.Targets;

public sealed class ProcessTargetObserverTests
{
    [Fact]
    public async Task WaitForAsync_returns_success_when_module_already_loaded()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ProcessTargetObserver observer = new();
        RecipeTarget target = RecipeTarget.ForProcessId(Environment.ProcessId);
        WaitCondition condition = new(WaitConditionKind.ModuleLoaded, "kernel32.dll", 1000);

        WaitResult result = await observer.WaitForAsync(target, condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_times_out_for_missing_file()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ProcessTargetObserver observer = new();
        RecipeTarget target = RecipeTarget.ForProcessId(Environment.ProcessId);
        WaitCondition condition = new(WaitConditionKind.FileExists, @"C:\nope\missing.bin", 200);

        WaitResult result = await observer.WaitForAsync(target, condition);

        Assert.False(result.Success);
        Assert.Equal("WAIT_CONDITION_TIMEOUT", result.ErrorCode);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_process_exit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ProcessStartInfo psi = new()
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using Process proc = Process.Start(psi)!;

        ProcessTargetObserver observer = new();
        RecipeTarget target = RecipeTarget.ForProcessId(proc.Id);
        WaitCondition condition = new(WaitConditionKind.ProcessExited, string.Empty, 2000);

        WaitResult result = await observer.WaitForAsync(target, condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_named_pipe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string pipeName = $"runewire-pipe-{Guid.NewGuid():N}";
        using NamedPipeServerStream pipe = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        ProcessTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.NamedPipeAvailable, pipeName, 2000);

        WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_named_event()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string eventName = $"Global\\runewire-event-{Guid.NewGuid():N}";
        using EventWaitHandle evt = new(false, EventResetMode.AutoReset, eventName);

        ProcessTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.NamedEvent, eventName, 2000);

        WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_shared_memory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string mapName = $"runewire-map-{Guid.NewGuid():N}";
        using var mmf = MemoryMappedFile.CreateNew(mapName, 1024);

        ProcessTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.SharedMemoryExists, mapName, 2000);

        WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_shared_memory_value()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string mapName = $"runewire-map-{Guid.NewGuid():N}";
        using var mmf = MemoryMappedFile.CreateNew(mapName, 1024);
        using (MemoryMappedViewStream stream = mmf.CreateViewStream())
        using (StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("ready");
            writer.Flush();
        }

        ProcessTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.SharedMemoryValueEquals, $"{mapName}=ready", 2000);

        WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WaitForAsync_succeeds_for_tcp_port()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        ProcessTargetObserver observer = new();
        WaitCondition condition = new(WaitConditionKind.TcpPortListening, $"127.0.0.1:{port}", 2000);

        WaitResult result = await observer.WaitForAsync(RecipeTarget.Self(), condition);

        Assert.True(result.Success);
        listener.Stop();
    }
}
