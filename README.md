# jaindb
JainDB is a blockchain based data archive for JSON data. It provides a [REST API](https://github.com/rzander/jaindb/wiki/REST-API)  to interact with the data store. Jaindb is using REDIS, CosmosDB or just the File-System to store the data by using hashing and deduplication technologies.

Jaindb was initially created to store inventory data of computers in an auditable way with the history of all changes. But as jaindb is schema-less, it can store everything you want... 

## Data Store
Jaindb can store data fragments on:
- File System (slow but simple)
- Redis Cache (fast and easy to setup)
- CosmosDB (scalable cloud service)

It splits the original data into blocks (JSON Objects) that can be referenced from other data sources..
An example on inventory data of an audio device on a computer:
```
"Audio": [
    {
      "##hash": "9qZnR1RLHiwJK1tbVpfrAkDX5"
    },
    {
      "##hash": "9qZpHGtxLiUYSaoZ3QGeXum35"
    }
  ]
```
this device has two audio devices, but just the hash values are stored on the audio object, so other computers with the same audio device do not have to story any additional data, just link to the existing data which is the reference to the hash value.

at the end, the hash value of the inventory data that contains all the hash references will be stored and added to a blockchain:
```
{
  "Chain": [
    {
      "index": 0,
      "timestamp": 636471513240444298,
      "previous_hash": "",
      "hash": "x3gVMwVt+Snzj8b9Gln8sp4Ujw/E2jtX6KfcEzWluhA=",
      "nonce": 1,
      "data": "",
      "signature": "",
      "blocktype": "root"
    },
    {
      "index": 1,
      "timestamp": 636471513242139314,
      "previous_hash": "x3gVMwVt+Snzj8b9Gln8sp4Ujw/E2jtX6KfcEzWluhA=",
      "hash": "/mJCe9E/OukY3udxiMt6yLpFQ79dNn66qc5iHCT1AUU=",
      "nonce": 2,
      "data": "9qZNqUJ2H3Jso71NEPc6FCQyS",
      "signature": "",
      "blocktype": "INV"
    },
    {
      "index": 2,
      "timestamp": 636472149162185683,
      "previous_hash": "/mJCe9E/OukY3udxiMt6yLpFQ79dNn66qc5iHCT1AUU=",
      "hash": "Tst3z3gdq2krnDzj4R6DpeAUwFbN1GzbmrSe5il0NSc=",
      "nonce": 3,
      "data": "9qZQ2AxdCqkz15nyMTLxyGkii",
      "signature": "",
      "blocktype": "INV"
    }
  ]
}
```
If something changes on the asset data, a new block with a reference to the list of all hashed inventory data ("data" attribute) will be added to the blockchain.


> Storing data in hashed blocks is great to save disk space but it requies an API to convert the data back into a readable format. JainDB provides a REST API to upload and query data. https://github.com/rzander/jaindb/wiki/REST-API

Docker Image: https://hub.docker.com/r/zanderr/jaindb/
