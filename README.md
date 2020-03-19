# FaceitMatchGatherer
Polls the Faceit API for new demos of users.

## Environment Variables

- `FACEIT_OAUTH_CLIENT_SECRET` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow [*]
- `FACEIT_OAUTH_CLIENT_ID` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow [*]
- `FACEIT_OAUTH_TOKEN_ENDPOINT` : 
Required for [Faceit's OAuth2](https://developers-support.faceit.com/hc/en-us/articles/115001594504-FACEIT-Connect-Documentation) Authorization Code Flow
- `FACEIT_API_KEY` : 
Required for communication with [Faceit's Data API](https://developers.faceit.com/docs/tools/data-api) [*]
- `MYSQL_CONNECTION_STRING` : Connection string to FaceitMatchGatherer DB[*]
- `AMQP_URI` : URI to the rabbit cluster [*]
- `AMQP_FACEIT_QUEUE` : Rabbit queue's name for producing messages to DemoCentral [*]

[*] *Required*

## Additional Information
- Upon Startup, this project runs migrations on its database.