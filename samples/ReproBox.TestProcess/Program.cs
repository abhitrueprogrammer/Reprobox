using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;

Console.WriteLine($"PID: {Environment.ProcessId}");

// Two regular files
using var file1 = File.Open("file1.txt", FileMode.OpenOrCreate);
using var file2 = File.Open("file2.txt", FileMode.OpenOrCreate);

// Pipe
using var pipeServer = new AnonymousPipeServerStream(
    PipeDirection.Out,
    HandleInheritability.None);

using var pipeClient = new AnonymousPipeClientStream(
    PipeDirection.In,
    pipeServer.ClientSafePipeHandle);

// TCP listener
var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();

int port = ((IPEndPoint)listener.LocalEndpoint).Port;

// TCP connection to ourselves
using var client = new TcpClient();
client.Connect(IPAddress.Loopback, port);

using var accepted = listener.AcceptTcpClient();

Console.WriteLine($"Listening on 127.0.0.1:{port}");
Console.WriteLine("Resources are open. Sleeping for 60 seconds...");

Thread.Sleep(Timeout.Infinite);

listener.Stop();
