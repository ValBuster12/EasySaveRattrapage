# EasySave
![Version 1.0](https://img.shields.io/badge/Version-1.0-green) ![Project framework .NET](https://img.shields.io/badge/Project%20framework-.NET-purple)

[🔧 Technical documentation 🔧](https://crioschan.github.io/EasySave/)

EasySave is a backup automation tool with:
- a local GUI + CLI host app (`EasySave`),
- a TCP broker (`EasySave.Server`),
- a remote supervision UI (`EasySave.RemoteConsole`),
- a shared message contract layer (`EasySave.Protocol`).

## Architecture

### Projects
- `EasySave`: backup execution host (Avalonia + MVVM + CLI fallback).
- `EasySave.Server`: lightweight TCP broker (message routing only, no backup business logic).
- `EasySave.RemoteConsole`: remote monitoring/command UI.
- `EasySave.Protocol`: shared envelope/message contracts + serializer + transport framing.

### Remote-console architecture (high level)
1. `EasySave` host registers to broker with a stable `instanceId`.
2. `RemoteConsole` registers and optionally subscribes to one `instanceId`.
3. Broker routes host telemetry (`snapshot`, `progress`, `command.result`) to subscribed consoles.
4. Broker routes remote `command.request` to the targeted host.

## How to start the server

### Default
```bash
dotnet run --project EasySave.Server
```

### Custom endpoint
```bash
dotnet run --project EasySave.Server -- --ip 0.0.0.0 --port 5000
```

The server also supports environment variables:
- `EASYSAVE_SERVER_IP`
- `EASYSAVE_SERVER_PORT`
- `EASYSAVE_SERVER_ROUTING_MODE`

## Run EasySave in local mode vs remote mode

### Local mode (no remote console)
- Start EasySave only:
```bash
dotnet run --project EasySave
```
- Use GUI or CLI as usual:
```bash
dotnet run --project EasySave -- 1-3
dotnet run --project EasySave -- 1;3
```

### Remote mode (host connected to broker)
1. Start broker (`EasySave.Server`).
2. Start EasySave (`EasySave`) with server settings configured in app settings.
3. EasySave host registers and publishes job updates to broker.
4. Remote consoles can subscribe and send commands.

## How to start the remote console

```bash
dotnet run --project EasySave.RemoteConsole
```

Then:
1. Enter broker IP/port.
2. Click connect.
3. Select or type a host instance id.
4. Subscribe and send commands (start/pause/resume/stop).

## Procédure complète : lancer un job à distance (Remote Control)

> Cette procédure couvre le flux complet `EasySave.Server` + hôte `EasySave` + `EasySave.RemoteConsole`.

### 1) Démarrer le broker TCP
```bash
dotnet run --project EasySave.Server -- --ip 127.0.0.1 --port 5000
```

### 2) Configurer l'hôte EasySave pour le mode distant
Dans l'application hôte (`EasySave`), ouvrez les paramètres réseau et vérifiez :
- IP du serveur broker : `127.0.0.1` (ou IP de la machine broker),
- Port : `5000`,
- Mode de routage activé côté serveur (valeur par défaut recommandée).

Ensuite lancez l'hôte :
```bash
dotnet run --project EasySave
```

### 3) Créer/charger au moins un job de sauvegarde sur l'hôte
Depuis l'UI EasySave, ajoutez un job (nom + source + cible + type).

### 4) Démarrer la console distante
```bash
dotnet run --project EasySave.RemoteConsole
```

Dans la Remote Console :
1. Saisissez IP/port du broker (`127.0.0.1:5000` en local),
2. Cliquez **Connect**,
3. Sélectionnez l'instance hôte détectée,
4. Vérifiez que les jobs apparaissent (snapshot),
5. Envoyez la commande (**Start**, **Pause**, **Resume**, **Stop**).

### 5) Vérifications si une commande ne part pas
- L'hôte et la console sont connectés au **même broker** (même IP/port).
- L'instance choisie dans la console correspond bien à l'ID d'instance de l'hôte.
- Le job existe bien sur l'hôte (ID valide).
- Pour `Resume`, le job doit être en pause.
- Pour `Stop`, le job doit être en cours d'exécution.
- Pour `Start`, le job doit être inactif (idle/stopped/completed) et pas déjà en cours.

### 6) Vérifications réseau rapides
```bash
dotnet run --project EasySave.Server -- --ip 0.0.0.0 --port 5000
dotnet run --project EasySave
dotnet run --project EasySave.RemoteConsole
```

Si les machines sont différentes :
- ouvrez le port TCP broker dans le pare-feu,
- utilisez l'IP LAN/WAN correcte au lieu de `127.0.0.1`.

## Message flow overview

### Registration
- Host -> Broker: `host.registration`
- Remote Console -> Broker: `remoteConsole.registration`

### Telemetry
- Host -> Broker: `backupJob.snapshot`
- Host -> Broker: `backupJob.progressUpdate`
- Broker -> Subscribed remotes: same telemetry messages

### Commands
- Remote Console -> Broker: `command.request`
- Broker -> Target host: `command.request`
- Host -> Broker: `command.result`
- Broker -> Subscribed remotes: `command.result`

### Errors
- Broker -> Client: `connection.error`

## UML
- Full source: `docs/uml/EasySave-full.puml`
- Regenerate:
```bash
dotnet run --project tools/UmlGenerator/UmlGenerator.csproj -- --config Debug
```

## Contribution
See [CONTRIBUTING.md](CONTRIBUTING.md).

## License
All rights reserved by ProSoft (fictional company).
