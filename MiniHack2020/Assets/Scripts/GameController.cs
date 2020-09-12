using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using TMPro;
using UnityEditor.UIElements;
using UnityStandardAssets.Characters.FirstPerson;
using System.IO;
using Microsoft.WindowsAzure.Storage.File;

public class GameController : MonoBehaviour
{
    public GameObject player;

    public string connectionString;
    public string messagesTable;
    public string counterTable;
    public string paintingsShare;
    public CloudStorageAccount StorageAccount;
    List<MessageEntity> tempTableResult;

    public GameObject messagPrefab;
    public GameObject messagesParent;

    public GameObject messageUI;

    public SpriteRenderer painting;
    public GameObject paintingUI;
    public GameObject paintingStuff;

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
        if (Input.GetKeyDown(KeyCode.P))
            EnablePainting();
        if (Input.GetKeyDown(KeyCode.Escape) && paintingUI.activeSelf)
            DisablePainting();
    }

    void EnableMessageUI()
    {
        messageUI.SetActive(true);
        DisablePlayerStuff();
    }

    void DisableMessageUI()
    {
        messageUI.SetActive(false);
        EnablePlayerStuff();
    }

    void DisablePlayerStuff()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        player.GetComponent<FirstPersonController>().enabled = false;
    }

    void EnablePlayerStuff()
    {
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

    private async Task<CounterEntity> UpdateCounterEntity(string tableName, CounterEntity entity)
    {
        CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();

        // Create a table client for interacting with the table service 
        CloudTable table = tableClient.GetTableReference(tableName);

        // Create the InsertOrReplace  TableOperation
        TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(entity);

        // Execute the operation.
        TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
        CounterEntity insertedEntity = result.Result as CounterEntity;
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

    public async Task<CounterEntity> GetCounterEntity(string tableName, string partitionKey, string rowKey)
    {
        TableOperation retrieveOperation = TableOperation.Retrieve<CounterEntity>(partitionKey, rowKey);
        CloudTableClient tableClient = StorageAccount.CreateCloudTableClient();
        CloudTable table = tableClient.GetTableReference(tableName);

        TableResult result = await table.ExecuteAsync(retrieveOperation);
        CounterEntity counterEntity = result.Result as CounterEntity;
        return counterEntity;
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

    public void SavePainting()
    {
        EncondeSpritePNG(painting.sprite);
        UploadPainting();
    }

    void EnablePainting()
    {
        paintingUI.SetActive(true);
        paintingStuff.SetActive(true);
        DisablePlayerStuff();
    }

    void DisablePainting()
    {
        paintingUI.SetActive(false);
        paintingStuff.SetActive(false);
        EnablePlayerStuff();
    }

    public async void UploadPainting()
    {     
        // Create a file client for interacting with the file service.
        CloudFileClient fileClient = StorageAccount.CreateCloudFileClient();

        // Create a share for organizing files and directories within the storage account.
        CloudFileShare share = fileClient.GetShareReference(paintingsShare);

        try
        {
            await share.CreateIfNotExistsAsync();
        }
        catch (StorageException)
        {
            throw;
        }

        CloudFileDirectory root = share.GetRootDirectoryReference();

        CounterEntity paintingsCounter = await GetCounterEntity(counterTable, "PaintingsCounter", "PaintingsCounter");
        paintingsCounter.count = "" + (int.Parse(paintingsCounter.count) + 1);

        CloudFile file = root.GetFileReference("painting" + paintingsCounter.count + ".png");
        await file.UploadFromFileAsync(Application.persistentDataPath + "painting.png");
        await UpdateCounterEntity(counterTable, paintingsCounter);
    }

    public void EncondeSpritePNG(Sprite sprite)
    {
        Texture2D texture = sprite.texture;
        byte[] textureBytes = texture.EncodeToPNG();
        File.WriteAllBytes(Application.persistentDataPath + "painting.png", textureBytes);
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

public class CounterEntity : TableEntity
{
    public string count { get; set; }

    public CounterEntity() { }
}
