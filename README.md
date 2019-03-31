# Fetcho

It's for searching the internet

If you don't know where you're going, this is a great place to start.

Useful concepts to understand it:
1. How do you carve an elephant out of marble? Start with a block of marble and cut away everything that doesn't look like an elephant.

# Filters

| Filter              | Syntax                                                                     | Description                                                                                                                                                                                           |
|---------------------|----------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| hash:               | `hash:[md5_hash\|*][:md5_hash\|*]`                                           | Search by a md5 hash of the page data                                                                                                                                                                 |
| distinct-window():  | `distinct-window([domain\|ip\|title\|referer\|datahash]):window_size_in_pages` | Filter out duplicate pages by a specific property of the page. eg. domain, host, ip                                                                                                                   |
| filetype:           | `filetype:[filetype\|*][:filetype\|*]`                                       | Filter by specific file types eg text/html or html or javascript                                                                                                                                      |
| geo-ip-city:        | `geo-ip-city:[city\|*][:city\|*]`                                            | Filter by the geo IP city                                                                                                                                                                             |
| geo-ip-ll:          | `geo-ip-ll:[[[x,y],[x,y]]\|*][:[[x,y],[x,y]]\|*]`                            | Filter by the geo IP latitude and longtidue window                                                                                                                                                    |
| geo-ip-country:     | `geo-ip-country:[country\|*][:country\|*]`                                   | Filter by the geo IP country                                                                                                                                                                          |
| geo-ip-subdivision: | `geo-ip-subdivision:[subdivision\|*][:subdivision\|*]`                       | Filter by the geo IP subdivision                                                                                                                                                                      |
| has:                | `has:[property_name\|*]`                                                    | Filter pages that have a specific property set                                                                                                                                                        |
| lang:               | `lang:[lang\|*][:lang\|*]`                                                   | Filter pages by their language eg. en is english                                                                                                                                                      |
| ml-model():         | `ml-model(model_name[, confidence]):[classification\|*][:classifcation\|*]`  | Filter pages by a machine learning text classifier model. eg. ml-model(science,0.95):Health filters using the science model at a 95% confidence for Health related articles                           |
| random:             | `random:[probability][:*]`                                                 | Filter by a random number eg random:0.0001 will include 1 in 10000 pages.                                                                                                                             |
| property():         | `property(propertyName):[value match\|*][:value match\|*]`                   | Filter by pages that have a certain property. Properties are meta tags from pages. Where the properties include colon chars these will be replaced with underscores. eg. og:type property is og_type. |
| regex:              | `regex:[pattern|*][:tag_pattern|*]`                                        | Filter by regular expressions on the page content.                                                                                                                                                    |
| request-header():   | `request-header(key name):[value match\|*][:value match\|*]`                 | Filter by a request header property when the page was originally requested.                                                                                                                           |
| response-header():  | `response-header(key name):[value match\|*][:value match\|*]`                 | Filter by a response header property when the page was originally requested.                                                                                                                          |
| site:               | `site:[site\|*][:site\|*]`                                                   | Filter by the domain in the URI.                                                                                                                                                                      |
| tag:                | `tag:[tagfilter\|*][:replace_with]`                                         | Filter by a tag                                                                                                                                                                                       |
| uri:                | `uri:[uri_fragment\|*][:uri_fragment\|*]`                                    | Filter by some part of the URI.                                                                                                                                                                       |
| query():            | `query(access_key_id):[search text\|*][:search text\|*]`                     | Include another workspace query within this query.                                                                                                                                                    |
| xpath:              | `xpath:[xpath\|*][:xpath\|*]`                                                | Filter pages by an xpath query. NOTE NOT WORKING YET                                                                                                                                                  |

# Search query examples

Traditional word search
```
burger
```

Include a random page for every 10000 looked at
```
random:0.0001 
```

Search for burgers and filter by city
```
burger geo-ip-city:Austin
```

Filter by language
```
lang:en
```

Tag the results by city where that city is in the United_States
```
geo-ip-city:United_States:*
```

Exclude all english language pages
```
-lang:en
```

Remove 1 in 100000 random pages from the search even if all other pages match
```
burger -random:0.00001
```

# API

## End Points

Use accesskeys if you're building a user facing tool  
Use workspaces if you're building a server facing tool

* /api/v1/accounts - access all the data for an `Account`  
* /api/v1/accesskeys - access all the data by an `AccessKey`  
* /api/v1/workspaces - access all the data for a `Workspace`

### Accounts

### AccessKeys

Use this for user facing tools. You make up your own access keys minimum of 12 chars.

* GET /wellknown - get all well known `WorkspaceAccessKey`s  
* GET /{accesskey} - list all the `WorkspaceAccessKey`s for an `AccessKey`
* GET /{accesskey}/workspace/{Id} - get a specific `Workspace` by its `WorkspaceAccessKey` id
* GET /{accesskey}/workspace/{workspaceAccessKeyId}/results - get the `Workspace` `WorkspaceResults`. Optional ?fromSequence=&lt;number greater than 0&gt;&count=&lt;number:0-50&gt;
* GET /{accesskey}/workspace/{workspaceAccessKeyId}/results/random - Get a random result from the `Workspace`;
* GET /{accesskey}/workspace/{workspaceAccessKeyId}/supportedFilters - Get the search and tagging filters supported by this `Workspace`;

* POST / - create an `AccessKey`
* POST /{accesskey}/workspace - create a `Workspace` and owner `WorkspaceAccessKey`
* PUT /{accesskey}/workspace/{workspaceAccessKeyId} - update a `Workspace`
* DELETE /accesskeys/{accesskey}/workspace/{workspaceAccessKeyId} - delete a WorkspaceAccessKey. Note the `Workspace` wont delete until all `WorkspaceAccessKey`s that reference it are deleted
* PUT,POST /{accesskey}/workspace/{workspaceAccessKeyId}/results - add or update `WorkspaceResult`s for a `Workspace`
* DELETE /{accesskey} - delete an `AccessKey`

### Workspaces

Use this for server facing tools

* GET /wellknown - get all well known `Workspace`s. Useful for publicly accessible workspaces and community workspaces  
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
      "Expiry":"9999-12-31T00:00:00",  
      "IsActive":true,  
      "Permissions":1, // flags 0 = none, 1 = owner 
	  "IsWellKnown":true,
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
   "IsWellKnown":false,
   "ResultCount":206,  
   "AccessKeys":[    
      {  
         "Id":"8cd40e60-5749-480a-a0e3-77d66f3bb5d6",  
         "AccessKey":"PurpleMonkeyDishwasher",  
         "Expiry":"9999-12-31T00:00:00",  
         "IsActive":true,  
         "Permissions":1,
		 "Created":"2019-02-15T14:25:06.639414+08:00"  
      },  
      {    
         "Id":"ab57d44d-2e6b-4404-adfa-0a8e393ffd45",  
         "AccessKey":"FaxesFavouriteKey",  
         "Expiry":"9999-12-31T00:00:00",  
         "IsActive":true,  
         "Permissions":1,
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
    "RefererUri":"",  
    "Uri":"https://www.alibris.com/",  
    "Title":"Alibris - Buy new and used books, textbooks, music and movies",  
    "Description":"",  
    "Tags":[ 
		"en",
		"Austin",
		"United_States"
    ],  
    "Created":"2019-03-07T20:17:38.838743+08:00",  
    "PageSize":62985,  
    "Sequence":9241  
}
```
