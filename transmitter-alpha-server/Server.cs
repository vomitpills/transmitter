using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using transmitter_alpha_common;

namespace transmitter_alpha_server;

internal static class Program
{
    public static async Task Main()
    {
        Server server = new();
        server.Start(57181, 57182);
        await Task.Delay(Timeout.Infinite);
    }
}

internal class Transaction(string senderId, Mail mail)
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string SenderId { get; } = senderId;
    public Mail Mail { get; } = mail;
    public Func<Task>? Callback { get; private set; }

    public void SetCallback(Func<Task> callback) => Callback = callback;
}

public partial class Server
{
    private readonly List<string> validInvites = [];
    private readonly Dictionary<string, string> differedChannelKeysAndSecrets = []; // add expiration
    private readonly Dictionary<string, Profile> profiles = [];

    private readonly Dictionary<string, string> peerSecretToId = [];
    private IEnumerable<string> KnownPeerIds => peerSecretToId.Values;

    private readonly List<Task> peerCommConnections = [];
    private readonly List<string> peerDifferedConnections = [];

    private readonly Dictionary<string, Channel<Transaction>> backlog = [];

    bool isRunning = false;

    public void Start(int commPort, int updatePort)
    {
        if (isRunning)
            throw new InvalidOperationException("already running");
        isRunning = true;

        Task.Run(() => StartCommLoop(commPort));
        Task.Run(() => StartUpdateLoop(updatePort));
    }

    private static string GetNewSecret() => RandomNumberGenerator.GetHexString(64);
    private static string GetNewId() => RandomNumberGenerator.GetHexString(16);

    private Profile? CheckPeerState(Message incomingRequest, bool checkProfile = true) => CheckPeerState(incomingRequest, out _, checkProfile);

    private Profile? CheckPeerState(Message incomingRequest, out string auth, bool checkProfile = true)
    {
        auth = CheckAuth(incomingRequest);
        if (!checkProfile)
            return null;
        string id = peerSecretToId[auth];
        return CheckProfile(id) ?? throw new StateException("no-profile");
    }

    private string CheckAuth(Message incomingRequest)
    {
        string auth = incomingRequest["auth"];
        if (!peerSecretToId.ContainsKey(auth))
            throw new FaultyDataException("auth");
        return auth;
    }

    private Profile? CheckProfile(string id)
    {
        profiles.TryGetValue(id, out Profile? profile);
        return profile;
    }
}