# jihub

Command Tool to help exporting Issues from Jira and import them as GitHub Issues

## What will be imported

Jihub let's you specify a search query to be able to import only the Jira Issues of your needs, for further information see `How to run`.

The jira issues will be converted and imported as GitHub Issues. All labels associated with a Jira Issue will be created in GitHub and linked to the corresponding Github Issue. The `Fix Version/s` field will be created as Milestone in GitHub.

All other currently implemented labels will be imported within the description.

## How to use

Make sure you have the dotnet 6 runtime installed on your system. For further information visit [Dotnet](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

To be able to only import the needed Jira Issues you must specify a search query, to test your query and the given results please visit e.g. https://jira.example.com/issues/

Example Query: `project = Test AND status not in (Closed, Cancelled, Done, Resolved, "In progress", Inactive, "In Review", "USER READY")``

_To use the Query please make sure to html encode it. Quick google search should help finding a good online tool for that._

1. Creating a GitHub PAT (Personal Access Token)

- Navigate to https://github.com/settings/tokens
- Generate a new token.

2. Setup the configuration

- All following steps must be executed within the jihub.Worker directory.
- Open the appsettings.json file with the editor of your choice.
- Fill all the values that are currently left empty and safe.
    - (`Github -> Token` must be filled with the generated token)

3. Restore all packages and dependencies

> dotnet restore

4. Build the solution

> dotnet build

5. Run the application

> dotnet run -r {Target Repository} -o {Repository Owner} -q {Encoded Query}