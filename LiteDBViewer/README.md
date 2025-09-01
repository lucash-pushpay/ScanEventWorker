# Scan Event Worker LiteDB Viewer for Mac

A simple command-line database viewer for the Scan Event worker for LiteDB databases, specifically created for macOS users.

## Why This Tool Exists

The official LiteDB Studio and other GUI tools for LiteDB are not natively supported on macOS, making it difficult to inspect and query LiteDB databases during development on Mac. This lightweight console application provides essential database inspection capabilities for Mac developers.

## Features

- 🔍 **Auto-discovery**: Automatically finds `.db` files in your project
- 📊 **Collection Overview**: Lists all collections with record counts
- 📦 **Scan Events Viewer**: Specialized display for scan event data
- 📋 **Parcel States Viewer**: Detailed parcel state information
- 🔧 **Interactive Mode**: Query and explore your database interactively
- 🍎 **Mac Native**: Built specifically for macOS development workflow

## Setup

- If getting the error: ```No .db files found```, update the absoluteDbPath in the Program.cs file to point to the ```scanevents.db``` generated under the ```scanEventWorker/bin/Debug/net8.0/``` directory.
- Every time new events are processed, the restart of the LiteDBViewer app is required to reload the latest data.
