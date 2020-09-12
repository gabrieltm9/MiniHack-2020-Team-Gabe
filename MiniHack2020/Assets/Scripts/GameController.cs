using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using TMPro;
using UnityEditor.UIElements;
using UnityStandardAssets.Characters.FirstPerson;

public class GameController : MonoBehaviour
{
    public GameObject player;

    public string connectionString;
    public string messagesTable;
    public CloudStorageAccount StorageAccount;
    List<MessageEntity> tempTableResult;

    public GameObject messagPrefab;
    public GameObject messagesParent;

    public GameObject messageUI;

    private void Awake()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        StorageAccount = CloudStorageAccount.Parse(connectionString);
        SpawnMessages();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.M) && !messageUI.activeSelf)
            EnableMessageUI();
        if (Input.GetKeyDown(KeyCode.Escape) && messageUI.activeSelf)
            DisableMessageUI();
    }

    void EnableMessageUI()
    {
        messageUI.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        player.GetComponent<FirstPersonController>().enabled = false;
    }

    void DisableMessageUI()
    {
        messageUI.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        player.GetComponent<FirstPersonController>().enabled = true;
    }

    public async void SpawnMessages()
    {
        await PartitionScanAsync(messagesTable); //Pulls entire messages table into tempTableResult list

        foreach (MessageEntity messageEntity in tempTableResult)
            InstantiateMessage(messageEntity);
    }

    void InstantiateMessage(MessageEntity messageEntity)
    {
        string[] posStrings = messageEntity.RowKey.Split(' ');
        float x = float.Parse(posStrings[0]);
        float y = float.Parse(posStrings[1]);
        float z = float.Parse(posStrings[2]);
        Vector3 pos = new Vector3(x, y, z);

        GameObject newMessage = Instantiate(messagPrefab, pos, transform.rotation, messagesParent.transform);
        newMessage.transform.GetChild(0).GetComponent<TMP_Text>().text = messageEntity.PartitionKey;
        newMessage.GetComponent<MessageController>().player = player;
    }

    public void AddMessageButton(TMP_InputField messageInput)
    {
        AddMessage(messageInput.text, player.transform.position);
        DisableMessageUI();
    }

    public async void AddMessage(string message, Vector3 pos)
    {
        CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();

        // Create a table client for interacting with the table service 
        CloudTable table = tableClient.GetTableReference(messagesTable);

        try
        {
            await table.CreateIfNotExistsAsync();
        }
        catch (StorageException)
        {
            throw;
        }

        MessageEntity messageEntity = new MessageEntity(message, pos);
        InstantiateMessage(messageEntity);
        await InsertOrMergeEntityAsync(table, messageEntity);
        Debug.Log("Message Added: " + message + " at " + pos);
    }

    private async Task<MessageEntity> InsertOrMergeEntityAsync(CloudTable table, MessageEntity entity)
    {
        // Create the InsertOrReplace  TableOperation
        TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(entity);

        // Execute the operation.
        TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
        MessageEntity insertedEntity = result.Result as MessageEntity;
        return insertedEntity;
    }

    public async Task<MessageEntity> GetEntityFromTable(string tableName, string partitionKey, string rowKey)
    {
        TableOperation retrieveOperation = TableOperation.Retrieve<MessageEntity>(partitionKey, rowKey);
        CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();
        CloudTable table = tableClient.GetTableReference(tableName);

        TableResult result = await table.ExecuteAsync(retrieveOperation);
        MessageEntity messageEntity = result.Result as MessageEntity;
        return messageEntity;
    }

    public async Task PartitionScanAsync(string tableName)
    {
        tempTableResult = new List<MessageEntity>();
        TableQuery<MessageEntity> partitionScanQuery = new TableQuery<MessageEntity>();
        CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();

        // Create a table client for interacting with the table service 
        CloudTable table = tableClient.GetTableReference(tableName);

        TableContinuationToken token = null;
        // Page through the results
        try
        {
            do
            {
                TableQuerySegment<MessageEntity> segment = await table.ExecuteQuerySegmentedAsync(partitionScanQuery, token);
                token = segment.ContinuationToken;
                foreach (MessageEntity entity in segment)
                {
                    tempTableResult.Add(entity);
                }
            }
            while (token != null);
        }
        catch
        {
            Debug.LogError("Couldnt pull leaderboard table '" + tableName + "'");
            throw;
        }
    }
}
public class MessageEntity : TableEntity
{
    public MessageEntity() { }

    public MessageEntity(string message, Vector3 pos)
    {
        this.PartitionKey = message;
        this.RowKey = pos.x + " " + pos.y + " " + pos.z;
    }
}
