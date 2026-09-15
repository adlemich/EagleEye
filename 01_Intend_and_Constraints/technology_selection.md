# Technology Selection, technical Constraints, Development Approach

This file lists the technological choices and with that some constraints for the technical solution architecture.

## Implementation Language

This project shall be implement with .NET 10 and the MAUI framework for the mobile and MacOS applications.

- [.net 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.net MAUI](https://dotnet.microsoft.com/en-us/apps/maui)

The windows server application, which shall run as system service, may use all windows specifics to fulfill the requirements. The parent apps for iOS, Android and MacOS shall be implemented by using a single code base as much as possible. Only UI code may differ, so a shared library or component model is demanded to maximize code reuse.
All source code created shall follow best practices of .net development.

## Scripting

All sequences for installation, building, testing and other mechanisms shall be done in multi-platform powershell v7.6. Whenever we want to automate something, we use Powershell scripts.

- [Powershell](https://learn.microsoft.com/en-us/powershell/?view=powershell-7.6)

## Integration Architecture

We will use SignalR as main integration and communication layer between the parent mobile applications and the windows server application. It shall use local network connectivity over private WLAN / LAN mainly.

- [SignalR](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr)

Here we want 3 main patterns to be implemented with a single technology:

1: Data Queries - the parent mobile applications may query data from the server application, e.g. to display current configurations or show statistical data.
2: Events and update pushes - the server application on windows may update the mobile apps with new data or in case of events, e.g. when the kid tries to use a non-allowed application without polling.
3: Publish / Subscribe to multiple clients - e.g. to inform multiple client applications and keep everything in synch even when multiple mobile apps are connected to the windows server in parallel.

All data and object types which are transferred via SignalR define the interface specification between windows server and clients. This interface specification shall be treated as first class citizen - we follow an API first approach.

## Development and Test

- The main development machine will be a MacBook Air or latest MacOS with XCode, VSCode, Powershell, VMWare Fusion installed locally.
- Testing of the windows server service application will occurr in Windows 11 Virtual Machine which is hosted on the developer machine within VMWare Fusion.
- Testing of the MacOS parent application shall occurr on the local development machine (connecting to the server on the virtual machine)
- This pair - Windows 11 in VM and MacOS parent application - shall be the primary test setup for all feature and quality testing.
- The iOS and Android applications shall be created in a later stage, after the MacOS application and the windows server application matured enough.
- To maximize reuse, we will develop the shared libraries that shall be used in all 3 parent mobile applications explicitly as shared components (own folder in source control)
- Further technological decision will be defined in the development project (e.g. test framework, UI testing, etc...)

## Git

We will use Git as source control system in a dedicated repository. The account is:

- [Git Account URL](https://github.com/adlemich)

Credentials will be shared in the dev environment and must never be stored in Git nor shall they be logged in log files or similar.

## Incremental Development

We will use an iterative and incremental development approach. While we collect all architecture significant requirements early, we will implement in a step by step approach. Each step defines, implements and verifies / test a single user story. So, the break down and refinement of user stories is always the first step, followed by an impact analysis to the system architecture which results and an implementation plan based on status quo. Finally the implementation, test and documenation of all changes and test results. The key here, is that each user story needs to complete this flow before we start a new user story.

## Component Architecture

When building or applications, we follow the principle of seperation of concerns. This means, that a single class will serve a single concern. It also means that we build a component architecture which follows a breakdown of functional areas (e.g. statistics collection is a component that has a clear scope and responsibility).

## Technical Decision Log

We will write down and log all technical decisions, from technology selection over component breakdown to pattern usage etc. Also all artifacts in the incremental process of user story implementation are stored in the project (e.g. implementation plans). Nothing is lost and we build a complete history of all changes and decisions from the inital generation of the project folder.

## IDE

We will use VSCode as mein IDE Tool.

## Markdown

Markdown files are the primary choice when it comes to all sorts of documentation. AI instructions, design documents, decision logs, user story descriptions, all in md files.

## PlantUML

We will use PlantUML as primary tool to integrate UML Diagrams into markdown files. We will run a local PlantUML server in a docker container on the local developer machine.

## AI Agents

We will use claude code v2.1 for this project. The default model to use is claude-opus-5.
