# fetcho

It's for searching the internet

If you don't know where you're going, this is a great place to start.

Useful concepts to understand it:
1. How do you carve an elephant out of marble? Start with a block of marble and cut away everything that doesn't look like an elephant.


# API

## Models

AccessKey - An indirect way to access the internal resources of the database  
Workspace - A group of a query, results and other configuration for a persistent search  
WorkspaceResult - Individual items searched for  

## End Points

Use accesskeys if you're building a user facing tool  
Use workspaces if you're building a server facing tool

* /api/v1/accesskeys - access all the data by an AccessKey  
* /api/v1/workspaces - access all the data for a Workspace

### AccessKeys

Use this for user facing tools

* GET /{accesskey} - get the details for an AccessKey
* GET /{accesskey}/workspace/{accessKeyId} - get a specific Workspace by its AccessKey id
* POST /{accesskey}/workspace - create a Workspace
* PUT /{accesskey}/workspace/{accessKeyId} - update a Workspace
* DELETE /accesskeys/{accesskey}/workspace/{accessKeyId} - delete a Workspace AccessKey. Note the workspace one delete until all accesskeys are deleted
* GET /{accesskey}/workspace/{accessKeyId}/results - get the Workspace WorkspaceResults. Optional ?minsequence=&lt;number greater than 0&gt;&count=&lt;number:0-50&gt;
* PUT,POST /{accesskey}/workspace/{accessKeyId}/results - add or update WorkspaceResult s for a workspace

### Workspaces

Use this for server facing tools

* GET /{workspaceId}/results - get WorkspaceResult records. Optional ?minsequence=&lt;number greater than 0&gt;&count=&lt;number:0-50&gt;
* PUT,POST /{workspaceId}/results - add or update WorkspaceResult records
* DELETE /{workspaceId}/results - delete WorkspaceResult records
