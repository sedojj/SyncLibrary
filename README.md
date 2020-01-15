# IntercomConversationSearch
Library provides code which allows to synchronize conversations from Intercom to prepared Kentico Kontent project and synchronize data to Algolia search index.
## How to build
Download/fork repository, open in VS, save all (will create SLN file for you). VS should automatically download necessary NuGet package dependencies. Add this to your project dependencies either as DLL or additional project.
## How to use
The `SearchProjectClient` class is the main class of the library. Using this class, you can run the synchronization.

To create an instance of the class, you need to provide a multiple settings classes.

```csharp
            AlgoliaSettings algoliaSettings = new AlgoliaSettings()
            {
                ApplicationId = <Algolia application id>,
                ApiKey = <Algolia API key>,
                IndexName = <Algolia index name>
            };

            KontentFunctionsSettings kontentSettings = new KontentFunctionsSettings()
            {
                ProjectId = <Kontent project ID>,
                CMApiKey = <Kontent project CM API key>,
                ConversationTypeGuid = <GUID of Conversation Model>,
                UserTypeGuid = <GUID of User Model>,
                CleanProject = <true/false, true only for first import>,
                BannedConversations = <list of conversation ids separated by comma>
            };

            SearchProjectClient client = new SearchProjectClient(<Intercom Auth Api Key>, kontentSettings, algoliaSettings);
```

Once you create a `SearchProjectClient`, use.
