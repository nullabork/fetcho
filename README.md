# fetcho

It's for searching the internet

If you don't know where you're going, this is a great place to start.

Useful concepts to understand it:
1. How do you carve an elephant out of marble? Start with a block of marble and cut away everything that doesn't look like an elephant.


# API

## End Points

Use accesskeys if you're building a user facing tool  
Use workspaces if you're building a server facing tool

* /api/v1/accesskeys - access all the data by an AccessKey  
* /api/v1/workspaces - access all the data for a Workspace

### AccessKeys

Use this for user facing tools. You make up your own access keys minimum of 12 chars.

* GET /{accesskey} - list all the `WorkspaceAccessKey`s for an `AccessKey`
* GET /{accesskey}/workspace/{workspaceAccessKeyId} - get a specific `Workspace` by its `WorkspaceAccessKey` id
* GET /{accesskey}/workspace/{workspaceAccessKeyId}/results - get the `Workspace` `WorkspaceResults`. Optional ?fromSequence=&lt;number greater than 0&gt;&count=&lt;number:0-50&gt;

* POST / - create or update an `AccessKey`
* POST /{accesskey}/workspace - create a `Workspace` and owner `WorkspaceAccessKey`
* PUT /{accesskey}/workspace/{workspaceAccessKeyId} - update a `Workspace`
* DELETE /accesskeys/{accesskey}/workspace/{workspaceAccessKeyId} - delete a WorkspaceAccessKey. Note the `Workspace` wont delete until all `WorkspaceAccessKey`s that reference it are deleted
* PUT,POST /{accesskey}/workspace/{workspaceAccessKeyId}/results - add or update `WorkspaceResult`s for a `Workspace`

### Workspaces

Use this for server facing tools

* GET /{workspaceId}/results - get `WorkspaceResult` records. Optional ?fromSequence=&lt;number greater than 0&gt;&count=&lt;number:0-50&gt;
* PUT,POST /{workspaceId}/results - add or update `WorkspaceResult` records
* DELETE /{workspaceId}/results - delete `WorkspaceResult` records

## Models

### AccessKey 
Keys for accessing workspaces  

```
{
	"Key": "PurpleMonkeyDishwasher",
	"Created": "2019-02-15T14:25:06.639414+08:00",  
	"IsActive": true
}
```

### WorkspaceAccessKey 
An access key to a Workspace Workspaces 

```
{  
      "Id":"8cd40e60-5749-480a-a0e3-77d66f3bb5d6",  
      "AccessKey":"PurpleMonkeyDishwasher",  
      "IsOwner":true,  
      "Expiry":"9999-12-31T00:00:00",  
      "IsActive":true,  
      "IsRevoked":false,  
      "Created":"2019-02-15T14:25:06.639414+08:00"  
}  
```

### Workspace 
Details of a running search query and it's results and other configuration for a persistent search  

``` 
{  
   "WorkspaceId":"f5201ff7-ea59-4e00-87b9-af4a0a9c8e2e",  
   "Name":"random stuff",  
   "Description":"",  
   "QueryText":"-random",  
   "Created":"2019-02-15T06:25:06.639414+08:00",  
   "IsActive":true,  
   "ResultCount":206,  
   "AccessKeys":[    
      {  
         "Id":"8cd40e60-5749-480a-a0e3-77d66f3bb5d6",  
         "AccessKey":"PurpleMonkeyDishwasher",  
         "IsOwner":true,  
         "Expiry":"9999-12-31T00:00:00",  
         "IsActive":true,  
         "IsRevoked":false,  
         "Created":"2019-02-15T14:25:06.639414+08:00"  
      },  
      {    
         "Id":"ab57d44d-2e6b-4404-adfa-0a8e393ffd45",  
         "AccessKey":"FaxesFavouriteKey",  
         "IsOwner":false,  
         "Expiry":"9999-12-31T00:00:00",  
         "IsActive":true,  
         "IsRevoked":false,  
         "Created":"2019-03-08T17:39:50.25246+08:00"  
      }  
   ]  
}  
```

### WorkspaceResult 
Individual search results  

```  
{   
    "Hash":"DDD2291FFF76A7209D5F8BF2FD5EFAA6",  
    "ReferrerUri":"",  
    "Uri":"https://www.alibris.com/",  
    "Title":"Alibris - Buy new and used books, textbooks, music and movies",  
    "Description":"",  
    "Tags":[    
	  
    ],  
    "Created":"2019-03-07T20:17:38.838743+08:00",  
    "PageSize":62985,  
    "Sequence":9241  
}
```
