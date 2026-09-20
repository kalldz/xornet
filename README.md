```
██╗  ██╗ ██████╗ ██████╗ ███╗   ██╗███████╗████████╗
╚██╗██╔╝██╔═══██╗██╔══██╗████╗  ██║██╔════╝╚══██╔══╝
 ╚███╔╝ ██║   ██║██████╔╝██╔██╗ ██║█████╗     ██║   
 ██╔██╗ ██║   ██║██╔══██╗██║╚██╗██║██╔══╝     ██║   
██╔╝ ██╗╚██████╔╝██║  ██║██║ ╚████║███████╗   ██║   
╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝   ╚═╝    
```

# Xornet - Take Your Control Back

### Definition

"Xornet" is a superuser program that allows someone to control a network. Xornet is written in C# with Avalonia UI (cross-platform desktop GUI).
Xornet is targeted for release on Windows and Linux, with a focus on high optimization and stability. Xornet is still under development and still not stable yet.

### Features

Xornet have 4 core features: Scanner, Killer, Restore, and Defender.

- **Scanner** — a feature that scans a specific connected network. Scanner searches for connected devices across the available subnets.

- **Killer** — a feature that can block other devices on the network. Killer works by rapidly and continuously flooding the victim's data queries with empty packets using an ARP-spoofing technique (Denial of Service). Killer will keep attacking until the target's network connection is dropped.

- **Restore** — an action to stop Killer and return the network to its original state, so the target device reconnects to the network.

- **Defender** — a feature that protects devices from ARP-spoofing attacks. Defender locks the IP and MAC address of important devices such as the default gateway and router. When Defender detects a flood of spoofed packets from an attacker, it blocks that device so the user is not affected by the attack.

### How to run xornet


```
# Restore
dotnet restore

# Build
dotnet build

# Run Desktop GUI (need admin/root for Npcap/libpcap)
dotnet run --project src/Xornet.Desktop/Xornet.Desktop.csproj
```

## Support me

[Trakteer](https://trakteer.id/vinitkanov) (Indonesia)

[Saweria](https://saweria.co/vinitkanov) (Indonesia)

[Ko-fi](https://ko-fi.com/kalldz) (International)


