# Deepgram Java Starter

This sample demonstrates interacting with the Deepgram API from a Java server. It uses the Deepgram API with NanoHTTPD Java package to handle API calls, and has a React companion application to interact with the Java integration.

## Sign-up to Deepgram

Before you start, it's essential to generate a Deepgram API key to use in this project. [Sign-up now for Deepgram](https://console.deepgram.com/signup).

## Quickstart

### Manual

Follow these steps to get started with this starter application.

#### Clone the repository

Go to GitHub and [clone the repository](https://github.com/deepgram-starters/deepgram-python-starters).

#### Install dependencies

Install the project dependencies in the `Starter 01` directory.

```bash
cd ./Starter-01
```

#### Edit the config file

Copy the text from `.env-sample` and create a new file called `.env`. Paste in the code and enter your API key you generated in the [Deepgram console](https://console.deepgram.com/).

```bash
port=5000
deepgram_api_key=api_key
```

#### Run the application

Once running, you can [access the application in your browser](http://localhost:5000/).

```bash
javac -cp lib/java-json.jar:lib/java-dotenv-5.2.2.jar:lib/nanohttpd-2.2.0.jar:. Main.java
java -cp lib/java-json.jar:lib/java-dotenv-5.2.2.jar:lib/nanohttpd-2.2.0.jar:. Main
```
