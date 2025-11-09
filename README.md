# LogAlertingSystem

## Overview
System that ingest and parse system logs, applied create alert rules and can show alert which were created.

## What technologies was used
Backend: .NET/C# (version 8.0, choosed LTS version) 
Frontend: Blazor (Use this one framework, as it can be well integrated with solution and quickly developed together. Also have worked with it, and it good when require to create some POC with UI)
DB: SQL Lite (Use this DB as it is a lightweight, file-based database that could be ideal for POC project. And will not required any specific setup)

## How to run
1. Clone the project to the local computer
2. Install .net sdk 8.0 https://dotnet.microsoft.com/en-us/download/dotnet/8.0
3. Open terminal and locate to the folder where project located
4. Run command 'dotnet run --project src/LogAlertingSystem.Web/LogAlertingSystem.Web.csproj'
5. Register new user or use exist one with credentials below

### Already created user credentials:
Username: test@test.test
Password: Password1!

## Architecture
Here will provide overview on architecture and some related descions:

### Structure
src
- LogAlertingSystem.Web - contains UI and common files
- LogAlertingSystem.Domain - contain Entities which we will use in the project
- LogAlertingSystem.Infrastucture - contain db and repositories
- LogAlertingSystem.Application - contains interfaces and services
test
- LogAlertingSystem.Tests - contains unit tests

### Ingestion platforms
First of all want to mention that choose Windows platform, as it my main private workstation, where I could develope and test the solution. 
After my investigation I found that could be used 'EventLogReader' to automatically read the logs from the Windows Logs. This functionality provide a huge possibilities to read and filter events, without additional parsing data from separate files.

Also, work on tool for Linux and MacOs platforms, but unable to test it, so it can be done in future improvement.

### Processing
I build the system in next way:
- solution contain backgroung job, which run every 60 sec (defined in Constants) and get the fixed amount of logs
- store this logs
- check the exist alert rules
- depend on this alert rules it generate Alert and store to the DB

## Requirements
What was the requirement and how I cover it:
1. Implementing ingesting from at least one platform - Done.
2. Ingestion. The system should continuously read or tail logs from the chosen platform. - Done.
3. Alert Rules. Users can define rules in the UI. Each rule includes a condition, name. - Done.
4. Alert Generation. When a log entry matches a rule, the system should create and store an alert. - Done.
5. User Interface
The UI should allow:
a. Creating and managing alert rules - Done.
b. Viewing and filtering generated alerts - Done. 
c. Viewing and filtering raw log data - Done. 
d. Viewing a breakdown of alerts by severity - Done. Show statistic of alert and logs. 

## Future improvements
Here possible to implement a bunch of improvements, like:
- Choosing better way to store the the data, raw logs could be store in NoSQL solution
- Fully tested solution for each if platforms. 
- Covering with integration test, not only with unit. 
- Improving UI
- Dividing to different services to improve scallability. 