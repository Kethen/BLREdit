﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BLREdit.Game;

public class BLRServer : INotifyPropertyChanged
{
    private double ping = double.NaN;
    private bool isOnline = false;
    [JsonIgnore] public bool IsDefaultServer { get { return Equals(BLREditSettings.Settings.DefaultServer); } set { IsNotDefaultServer = value; OnPropertyChanged(); } }
    [JsonIgnore] public bool IsNotDefaultServer { get { return !IsDefaultServer; } set { OnPropertyChanged(); } }
    [JsonIgnore] public double Ping { get { return ping; } private set { ping = value; PingDisplay = ""; OnPropertyChanged(); } }
    [JsonIgnore] public string PingDisplay { get { return ping.ToString() + "ms"; } private set { OnPropertyChanged(); } }
    [JsonIgnore] public bool IsOnline { get { return isOnline; } private set { isOnline = value; IsNotOnline = value; OnPropertyChanged(); } }
    [JsonIgnore] public bool IsNotOnline { get { return !isOnline; } private set { OnPropertyChanged(); } }

    [JsonIgnore] private string serverName;
    public string ServerName { get { return serverName; } set { serverName = value; OnPropertyChanged(); } }
    public string ServerAddress { get; set; } = "localhost";
    public short Port { get; set; } = 7777;
    private string ipAddress;
    [JsonIgnore]
    public string IPAddress
    {
        get
        {
            if (ipAddress is null)
            {
                try
                {
                    var ip = Dns.GetHostEntry(ServerAddress);
                    foreach (IPAddress address in ip.AddressList)
                    {
                        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ipAddress = address.ToString();
                        }
                    }
                }catch { }
            }
            return ipAddress;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override bool Equals(object obj)
    {
        if (obj is BLRServer server)
        { return server.ServerAddress == ServerAddress && server.Port == Port; }
        else
        { return false; }
    }

    public void PingServer() 
    {
        if (IPAddress is null) { return; }
        Thread pingThread = new Thread(new ThreadStart(InternalPing));
        pingThread.Name = ServerAddress + " Ping";
        pingThread.Priority = ThreadPriority.Highest;
        pingThread.Start();
        //AutoResetEvent waiter = new(false);

        //Ping pingSender = new();

        //// When the PingCompleted event is raised,
        //// the PingCompletedCallback method is called.
        //pingSender.PingCompleted += new PingCompletedEventHandler(PingCompletedCallback);

        //// Create a buffer of 32 bytes of data to be transmitted.
        //string data = "BLREdit says Hello!!!!!!!!!!!!!!";
        //byte[] buffer = Encoding.ASCII.GetBytes(data);

        //// Wait 12 seconds for a reply.
        //int timeout = 12000;

        //// Set options for transmission:
        //// The data can go through 64 gateways or routers
        //// before it is destroyed, and the data packet
        //// cannot be fragmented.
        //PingOptions options = new(64, true);

        //// Send the ping asynchronously.
        //// Use the waiter as the user token.
        //// When the callback completes, it can wake up this thread.
        //pingSender.SendAsync(IPAddress, timeout, buffer, options, waiter);
        //LoggingSystem.LogInfo("Sent Ping Request");
    }

    private static byte[] msg = new byte[1] { 1 };
    private void InternalPing()
    {
        Stopwatch watch = new();
        IPEndPoint RemoteIpEndPoint = new(System.Net.IPAddress.Parse(IPAddress), Port);
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        int sent;
        byte[] array = new byte[16];
        var buffer = new ArraySegment<byte>(array);

        try
        {
            socket.SendTimeout = 2000;
            socket.ReceiveTimeout = 2000;
            socket.Connect(RemoteIpEndPoint);
            Task<SocketReceiveFromResult> recieve = socket.ReceiveFromAsync(buffer, SocketFlags.Partial, RemoteIpEndPoint);
            
            sent = socket.SendTo(msg, msg.Length, SocketFlags.None, RemoteIpEndPoint);
            watch.Start();
            recieve.Wait(2000);
            watch.Stop();

            Ping = watch.ElapsedMilliseconds;
            if (recieve.IsCompleted && !recieve.IsCanceled && !recieve.IsFaulted)
            {
                if (sent >= 1 && recieve.Result.ReceivedBytes >= 2)
                {
                    IsOnline = true;
                }
                else
                {
                    IsOnline = false;
                }
            }
            else
            {
                Ping = double.NaN;
            }
        }
        catch (ObjectDisposedException error)
        {
            LoggingSystem.LogError("{ObjectDisposedException}" + error.Message + "\n" + error.StackTrace);
            IsOnline = false;
            Ping = double.NaN;
        }
        catch (ArgumentOutOfRangeException error)
        {
            LoggingSystem.LogError("{ArgumentOutOfRangeException}" + error.Message + "\n" + error.StackTrace);
            IsOnline = false;
            Ping = double.NaN;
        }
        catch (AggregateException error)
        {
            LoggingSystem.LogError("{AggregateException}:");
            foreach (var ex in error.InnerExceptions)
            {
                LoggingSystem.LogError(ex.Message + "\n" + ex.StackTrace + "\n");
            }
            IsOnline = false;
            Ping = double.NaN;
        }
        LoggingSystem.LogInfo("Finished Ping for [" + ServerAddress + "]:" + Ping + "ms");
        socket.Close();
        socket.Dispose();
    }

    private void PingCompletedCallback(object sender, PingCompletedEventArgs e)
    {
        // If the operation was canceled, display a message to the user.
        if (e.Cancelled)
        {
            // Let the main thread resume.
            // UserToken is the AutoResetEvent object that the main thread
            // is waiting for.
            ((AutoResetEvent)e.UserState).Set();
        }

        // If an error occurred, display the exception to the user.
        if (e.Error != null)
        {
            LoggingSystem.LogError(e.Error.Message);
            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();
        }

        PingReply reply = e.Reply;

        DisplayReply(reply);

        // Let the main thread resume.
        ((AutoResetEvent)e.UserState).Set();
    }

    public void DisplayReply(PingReply reply)
    {
        if (reply == null)
        { LoggingSystem.LogInfo("No Reply Recieved"); return; } 
        
        if (reply.Status == IPStatus.Success)
        {
            IsOnline = true;
            Ping = reply.RoundtripTime / 1000.0D;
            LoggingSystem.LogInfo("[" + ServerAddress + "]:" + reply.Status + " (" + PingDisplay + ")");
        }
        else
        {
            LoggingSystem.LogInfo("[" + ServerAddress + "]:" + reply.Status);
        }
    }

    private ICommand launchServerCommand;
    [JsonIgnore]
    public ICommand LaunchServerCommand
    {
        get
        {
            if (launchServerCommand == null)
            {
                launchServerCommand = new RelayCommand(
                    param => this.LaunchClient()
                );
            }
            return launchServerCommand;
        }
    }

    public void LaunchClient()
    {
        if (BLREditSettings.Settings.DefaultClient is null)
        {
            //No Default Client selected
        }
        else
        {
            BLREditSettings.Settings?.DefaultClient?.LaunchClient(new LaunchOptions() { UserName = ExportSystem.ActiveProfile.PlayerName, Server = this });
        }
    }
}