# Deepgram .NET Starter

[![Discord](https://dcbadge.vercel.app/api/server/xWRaCDBtW4?style=flat)](https://discord.gg/xWRaCDBtW4)

This sample demonstrates interacting with the Deepgram API from a .NET server. It uses the Deepgram .NET SDK to handle API calls, and has a javascript client built from web components.

## Sign-up to Deepgram

Before you start, it's essential to generate a Deepgram API key to use in this project. [Sign-up now for Deepgram](https://console.deepgram.com/signup).

## Quickstart

### Manual

Follow these steps to get started with this starter application.

#### Clone the repository

Go to GitHub and [clone the repository](https://github.com/deepgram-starters/deepgram-csharp-starters).

#### Navigate into the project folder

Move into the project `./Starter-01`

```bash
cd ./Starter-01
```

#### Edit the config file

Copy the text from `.env-sample` and create a new file called `.env`. Paste in the code and enter your API key you generated in the [Deepgram console](https://console.deepgram.com/).

```bash
port=8080
deepgram_api_key=api_key
```

#### Run the application

Once running, you can [access the application in your browser](http://localhost:8080/).

```bash
dotnet run
```
