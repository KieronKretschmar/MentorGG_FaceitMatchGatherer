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
Required for communication with MentorInterface
- `MENTORINTERFACE_BASE_ADDRESS` : http address to mentor interface [*]
Required for the background service refreshing active users matches
- `MATCHES_LOOKER_MAX_USERS` : max amount of users to refresh in one interval - defaults to 20
- `MATCHES_LOOKER_PERIOD_DAYS` : interval in which to call match looker for a user refresh - defaults to 7 days
- `MATCHES_LOOKER_ACTIVITY_TIMESPAN` : interval in which users had to be active for matches to be recognized - defaults to 21 days
- `IS_MIGRATING` : set to "true" if you only want to run migrations without env vars configured
 
[*] *Required*

## Additional Information
- Upon Startup, this project runs migrations on its database.