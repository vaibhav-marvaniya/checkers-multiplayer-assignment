using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using TMPro;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private GameObject gameStartCanvas;
    [SerializeField] private GameObject gamePlayCanvas;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_Text statusLabel;

    [SerializeField] private Button twoPlayersButton;
    [SerializeField] private Button fourPlayersButton;
    [SerializeField] private TMP_Text PlayerCountTxt;
    public static int SelectedPlayerCount { get; private set; } = 2;

    [Header("LAN Discovery")]
    [SerializeField] private bool enableDiscovery = true;
    [SerializeField] private int discoveryPort = 47777;
    [SerializeField] private float discoveryListenTime = 3f;
    [SerializeField] private float hostBroadcastInterval = 1f;

    private Coroutine _clientConnectCoroutine;
    private Coroutine _clientDiscoveryCoroutine;
    private Coroutine _hostBroadcastCoroutine;

    private const string DiscoveryMessage = "CHECKERS_HOST";

    private void Awake()
    {
        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (clientButton != null) clientButton.onClick.AddListener(OnClientClicked);

        // NEW:
        if (twoPlayersButton != null)
            twoPlayersButton.onClick.AddListener(() => SetPlayerCount(2));
        if (fourPlayersButton != null)
            fourPlayersButton.onClick.AddListener(() => SetPlayerCount(4));

        SetPlayerCount(2);

        ShowStartUI();
        SetStatus("");

        if (enableDiscovery)
            _clientDiscoveryCoroutine = StartCoroutine(ClientDiscoveryRoutine());
    }


    private void OnDestroy()
    {
        if (hostButton != null) hostButton.onClick.RemoveListener(OnHostClicked);
        if (clientButton != null) clientButton.onClick.RemoveListener(OnClientClicked);

        if (twoPlayersButton != null) twoPlayersButton.onClick.RemoveAllListeners();
        if (fourPlayersButton != null) fourPlayersButton.onClick.RemoveAllListeners();
    }

    private void OnHostClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("No NetworkManager in scene.");
            // Debug.LogError("NetworkBootstrap: No NetworkManager.Singleton found.");
            return;
        }

        var transport = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            SetStatus("No UnityTransport on NetworkManager.");
            // Debug.LogError("NetworkBootstrap: Expected UnityTransport on NetworkManager.");
            return;
        }

        transport.ConnectionData.Address = "0.0.0.0";
        ushort port = transport.ConnectionData.Port;

        string myIp = GetLocalIPv4();
        // Debug.Log($"[HOST] Will listen on 0.0.0.0:{port}, Wi-Fi IP {myIp}");
        SetStatus($"Hosting at {myIp}:{port}");

        if (nm.StartHost())
        {
            // Debug.Log("Host started");
            ShowGameplayUI();

            if (enableDiscovery && _hostBroadcastCoroutine == null)
                _hostBroadcastCoroutine = StartCoroutine(HostBroadcastRoutine());
        }
        else
        {
            // Debug.LogError("Failed to start host");
            SetStatus("Failed to start host.");
        }
    }

    private void OnClientClicked()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("No NetworkManager in scene.");
            // Debug.LogError("NetworkBootstrap: No NetworkManager.Singleton found.");
            return;
        }

        var transport = nm.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            SetStatus("No UnityTransport on NetworkManager.");
            // Debug.LogError("NetworkBootstrap: Expected UnityTransport on NetworkManager.");
            return;
        }

        string ip = ipInputField != null ? ipInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(ip))
        {
            SetStatus("Enter host IP or wait for discovery.");
            // Debug.LogWarning("Client clicked without IP set.");
            return;
        }

        transport.ConnectionData.Address = ip;

        ushort port = transport.ConnectionData.Port;
        // Debug.Log($"[CLIENT] Connecting to {ip}:{port}");
        SetStatus($"Connecting to {ip}:{port}");

        if (nm.StartClient())
        {
            if (hostButton != null) hostButton.interactable = false;
            if (clientButton != null) clientButton.interactable = false;

            if (_clientConnectCoroutine != null)
                StopCoroutine(_clientConnectCoroutine);

            _clientConnectCoroutine = StartCoroutine(WaitForClientConnection(nm));
        }
        else
        {
            // Debug.LogError("Failed to start client");
            SetStatus("Failed to start client.");
        }
    }

    private IEnumerator WaitForClientConnection(NetworkManager nm)
    {
        const float timeout = 5f;
        float startTime = Time.time;

        ShowStartUI();

        while (Time.time - startTime < timeout)
        {
            if (!nm.IsClient)
            {
                // Debug.LogWarning("[CLIENT] No longer in client mode; aborting connect wait.");
                break;
            }

            if (nm.IsConnectedClient)
            {
                // Debug.Log("[CLIENT] Connected to host, switching to gameplay UI.");
                SetStatus("Connected.");
                ShowGameplayUI();

                if (hostButton != null) hostButton.interactable = true;
                if (clientButton != null) clientButton.interactable = true;

                _clientConnectCoroutine = null;
                yield break;
            }

            yield return null;
        }

        // Debug.LogWarning("[CLIENT] Failed to connect to host (timeout).");
        SetStatus("Could not connect. Check Wi-Fi / IP / port.");

        nm.Shutdown();
        ShowStartUI();

        if (hostButton != null) hostButton.interactable = true;
        if (clientButton != null) clientButton.interactable = true;

        _clientConnectCoroutine = null;
    }

    private IEnumerator ClientDiscoveryRoutine()
    {
        if (ipInputField == null)
            yield break;

        using (var udp = new UdpClient())
        {
            var listenEndPoint = new IPEndPoint(IPAddress.Any, discoveryPort);

            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.ExclusiveAddressUse = false;
                udp.Client.Bind(listenEndPoint);
            }
            catch (System.Exception e)
            {
                // Debug.LogWarning("NetworkBootstrap: failed to bind discovery listener: " + e.Message);
                yield break;
            }

            SetStatus("Searching for host");

            while (string.IsNullOrEmpty(ipInputField.text))
            {
                if (udp.Available > 0)
                {
                    IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref senderEndPoint);

                    string msg = System.Text.Encoding.UTF8.GetString(data);
                    if (msg == DiscoveryMessage)
                    {
                        string hostIp = senderEndPoint.Address.ToString();
                        ipInputField.text = hostIp;
                        // Debug.Log("Discovered host at " + hostIp);
                        SetStatus("Found host: " + hostIp);
                        break;
                    }
                }

                yield return null;
            }

            if (string.IsNullOrEmpty(ipInputField.text))
            {
                SetStatus("No host found. You can type IP manually.");
            }
        }

        _clientDiscoveryCoroutine = null;
    }

    private IEnumerator HostBroadcastRoutine()
    {
        using (var udp = new UdpClient())
        {
            udp.EnableBroadcast = true;
            var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(DiscoveryMessage);

            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                udp.Send(data, data.Length, broadcastEndPoint);
                yield return new WaitForSeconds(hostBroadcastInterval);
            }
        }

        _hostBroadcastCoroutine = null;
    }

    private void ShowStartUI()
    {
        if (gameStartCanvas != null) gameStartCanvas.SetActive(true);
        if (gamePlayCanvas != null) gamePlayCanvas.SetActive(false);
    }

    private void ShowGameplayUI()
    {
        if (gameStartCanvas != null) gameStartCanvas.SetActive(false);
        if (gamePlayCanvas != null) gamePlayCanvas.SetActive(true);
    }

    private void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.text = msg;

        // if (!string.IsNullOrEmpty(msg))
        //     Debug.Log("[STATUS] " + msg);
    }

    private string GetLocalIPv4()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        return unicast.Address.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            // Debug.LogWarning("GetLocalIPv4 failed: " + e.Message);
        }

        return "127.0.0.1";
    }
    private void SetPlayerCount(int count)
    {
        SelectedPlayerCount = Mathf.Clamp(count, 2, 4);
        PlayerCountTxt.text = "Player : " +count.ToString();
        // Optional: simple visual feedback
        if (twoPlayersButton != null)
            twoPlayersButton.interactable = (SelectedPlayerCount != 2);

        if (fourPlayersButton != null)
            fourPlayersButton.interactable = (SelectedPlayerCount != 4);

        // Debug.Log($"[Bootstrap] Selected player count: {SelectedPlayerCount}");
    }

}
