# FaceitMatchGatherer
Polls the Faceit API for new demos of users.

## HTTP Routes

### GET `/users/<steamId>`
Gets the database entry of the Faceit user with the given steamId.

### POST `/users/<steamId>`
Create a new Faceit user 

### DELETE `/users/<steamId>`
Removes User from database.

### POST `/users/<steamId>/look-for-matches`
Triggers calls to the Faceit API to find new matches of the specified user, and initiates the process of analyzing them.


## Enviroment Variables

- `FACEIT_OAUTH_CLIENT_SECRET` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow [*]
- `FACEIT_OAUTH_CLIENT_ID` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow [*]
- `FACEIT_OAUTH_TOKEN_ENDPOINT` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow [*]
- `FACEIT_API_KEY` : 
Required for communication with [Faceit's Data API](https://developers.faceit.com/docs/tools/data-api) [*]
- `MYSQL_CONNECTION_STRING` : Connection string to FaceitMatchGatherer DB[*]
- `AMQP_URI` : URI to the rabbit cluster [*]
- `AMQP_FACEIT_QUEUE` : Rabbit queue's name for producing messages to DemoCentral [*]

[*] *Required*
