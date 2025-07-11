# ARPergefactor

The ARPergefactor is a network monitor, that enables you to wake sleeping devices in your network on-demand, just by opening a connection to them, without having to apply a special configuration to either server nor client. It works by monitoring the local link for broad- and multicasts, which give away that a given device is going to be accessed. To prevent unwanted wake-ups from chatty network participants, there are plenty options to filter the incoming traffic.

## Who is this intended for?

The user group that benefits most from this, are amateur network architects, who run a HomeLab with any number of devices, except a single laptop. Maybe you have a dedicated web server for developing and testing customer projects, a homemade NAS topology, a virtual machine running the mighty GitLab, a rich Kubernetes cluster, a game streaming rig or anything imaginable that is composed of individual parts connected via Ethernet.

Also your endeavors must be limited at least either by the electricity bill or your awareness of climate change. Having only the devices powered on that are actually needed is not only cost efficient, but also smart.

## How does it work?

ARPergefactor is NOT a proxy, but may behave like one temporarily. If the program fails for whatever reason or the node which executes it goes down, you will experience a graceful degradation. The automatic wakeup won't work anymore, but you can still wake up your devices manually and continue to connect to them as usual. 

To achieve this, ARPergefactor listens for packets of IP resolution protocols to detect when a host want's to connect to another host. It then checks the configured mappings for a positive match to initiate a WakeRequest. This request will then first be filtered passively by the provided information of the sender (MAC and IP), to potentially discard the request early. If you configured ARPergefactor to also look for specific port service access and the targeted host is not responding, it will then actively impersonate that host for a brief period of time in order to listen for their unicast traffic. It then immediately reverts the IP configuration and will, if a request to the desired service was detected, wake the host with a Magic Packet sequence.

## Feature overview

- Uses libpcap to monitor low-level network traffic
- Can monitor different network interfaces independently
- Support for IPv4 (ARP) and IPv6 (NDP)
- Support for virtual machines
    - Connection attempts to a virtual hosts will wake their physical host instead
    - Traditional WakeOnLAN packets can also be redirected to the physical host
- Support for auto configuration
    - IP address detection via DNS
    - Router discovery via NDP
- Filter requests to be black- or whitelisted by
    - Source MAC address
    - Source IP address
    - Destination TCP/UDP port
    - Ping (ICMP echo request)
- Extensive logging

## Modes of operation

If you can call a low-power mini computer like a Raspberry PI your own, you can install ARPergefactor there in a central position to monitor the whole network and react to connection attempts from all hosts in the same broadcast domain.

But if you only have two devices, for example a laptop and a server to which you want to connect, you can also run ARPergefactor right there on your client and configure it to only react to connection attempts made by the very same device.

This also guarantees, that if you dwell in a bigger network topology, you can still wake your favourite devices on demand, without interfering with other hosts and clients on that network.

## Getting started

Please take a look at the [Wiki pages](https://github.com/MadWizardDE/ARPergefactor/wiki), where you can find all the information you need, in order to start using ARPergefactor.

## System Requirements

- **Windows 8+** or **macOS** or **Linux** (every distribution, where .NET runs)
- .NET 9 Runtime
- npcap (only on Windows)

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://coff.ee/MadWizardDE)
