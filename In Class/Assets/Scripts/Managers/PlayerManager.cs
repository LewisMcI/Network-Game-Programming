using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public delegate void PlayerAddedEventHandler(ulong clientId);
public delegate void PlayerRemovedEventHandler(ulong clientId);
public class PlayerManager : NetworkBehaviour
{
    // Singleton
    public static PlayerManager instance;

    // Player Delegates
    public event PlayerAddedEventHandler OnPlayerAdded;
    public event PlayerRemovedEventHandler OnPlayerRemoved;

    // Local Dictionary associating ClientID's and Player Data
    Dictionary<ulong, PlayerData> clientPlayerDictionary = new Dictionary<ulong, PlayerData>();

    // Server Dictionary associating ClientID's and Serialized Player Data
    Dictionary<ulong, string> serverPlayerDictionary = new Dictionary<ulong, string>();

    // Values
    public int GetPlayerCount() { return clientPlayerDictionary.Count; }
    public ulong GetPlayerId(int index)
    {
        int i = 0;
        foreach (var kvp in serverPlayerDictionary)
        {
            if (index == i)
                return kvp.Key;
            i++;
        }
        throw new Exception("Tried get PlayerID that does not exist");
    }
    public PlayerData GetPlayerData(ulong clientId)
    {
        return clientPlayerDictionary[clientId];
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerJoined;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerLeft;

        DontDestroyOnLoad(gameObject);
    }

    /* OnPlayerJoined
     * Called within the connected client and the server
     */
    private void OnPlayerJoined(ulong clientId)
    {
        if (!IsServer || clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Get Local Players PlayerData
            string playerDataJson = JsonSerializer.SerializePlayerData(Player.Instance.PlayerData);

            // Notify Server
            NotifyPlayerJoinedServerRpc(NetworkManager.Singleton.LocalClientId, playerDataJson);
        }
        else
            Debug.Log("Server: Player" + clientId + " Joined");
    }

    [ServerRpc(RequireOwnership=false)]
    private void NotifyPlayerJoinedServerRpc(ulong newPlayerClientId, string newSerializedPlayerData)
    {
        // Store Serverside
        serverPlayerDictionary.Add(newPlayerClientId, newSerializedPlayerData);

        // Pass through dictionary values to player.
        string[] players = new string[serverPlayerDictionary.Count];
        ulong[] clientIds = new ulong[serverPlayerDictionary.Count];
        int i = 0;
        foreach (var kvp in serverPlayerDictionary)
        {
            players[i] = kvp.Value;
            clientIds[i] = kvp.Key;
            i++;
        }

        string serializedPlayerClientIdList = JsonSerializer.SerializeUlongArray(clientIds);
        string serializedPlayerDataList = JsonSerializer.SerializeJsonArray(players);

        NotifyPlayerJoinedClientRpc(serializedPlayerClientIdList,  serializedPlayerDataList);
    }

    [ClientRpc]
    private void NotifyPlayerJoinedClientRpc(string serializedPlayerClientIdList, string serializedPlayerDataList)
    {
        // Deserialize PlayerData
        ulong[] playerClientIdList = JsonSerializer.DeserializeUlongArray(serializedPlayerClientIdList);
        string[] playerDataList = JsonSerializer.DeserializeJsonArray(serializedPlayerDataList);

        for (int i = 0; i < playerClientIdList.Length; i++)
        {
            if (clientPlayerDictionary.ContainsKey(playerClientIdList[i]))
                continue;
            PlayerData thisPlayerData = JsonSerializer.DeserializePlayerData(playerDataList[i]);

            AddPlayer(playerClientIdList[i], thisPlayerData);
        }
    }

    private void AddPlayer(ulong clientId, PlayerData playerData)
    {
        clientPlayerDictionary.Add(clientId, playerData);

        Debug.Log("Local: Player " + clientId + ", with Name: " + playerData.playerName + " has been Added");

        OnPlayerAdded?.Invoke(clientId);
    }

    /* OnPlayerLeft
    * Called within the connected client and the server
    */
    private void OnPlayerLeft(ulong clientId)
    {
        if (IsServer)
        {
            // Notify Clients
            NotifyPlayerLeftClientRpc(clientId);
            serverPlayerDictionary.Remove(clientId);
            Debug.Log("Server: Player" + clientId + " Left");
        }
    }

    [ClientRpc]
    private void NotifyPlayerLeftClientRpc(ulong clientId)
    {

        Debug.Log("Player Disconnected, closing game");
#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        RemovePlayer(clientId);
    }

    private void RemovePlayer(ulong clientId)
    {
        clientPlayerDictionary.Remove(clientId);

        Debug.Log("Local: Player " + clientId + " has been Removed");

        OnPlayerRemoved?.Invoke(clientId);
    }
}
