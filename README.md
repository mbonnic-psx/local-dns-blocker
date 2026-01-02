# Local DNS/Hosts Blocker (C#)

A C# command-line "DNS Blocker" that blocks specified domains by editing the **Hosts** file. 

> **Note:** This tool is a proof of concept and is not yet finished. If you wish to use this tool you will need to to have **Administrator** permissions on Windows, and change the path in the code on line 16 from ```@"drivers\etc\testHost.txt"``` to ```@"drivers\etc\hosts"```.

---

## Idea

- Block distracting or unwanted websites by mapping domains to a non-routable IP address (e.g., `0.0.0.0`).
- Because this uses the OS hosts file, the block applies system-wide (across browsers).
- This can be used as a local “parental control” style restriction: changing/removing entries requires Administrator access.

---

## Overview

### 1) Run the Program
- After running the program and giving admin access you will be prompted with the menu.
- You will be able to select one of the **7** options.

![menu](https://github.com/mbonnic-psx/local-dns-blocker/blob/master/screenshots/Menu.png)

### 2) List Host Entry
- This option will show the current Host file you have in your file explorer.

![listhostfile](https://github.com/mbonnic-psx/local-dns-blocker/blob/master/screenshots/ListHostFile.png)

### 3) Add Site to Host File
- This option will prompt the user to input a domain to block.
- Once entered it will apply to the Host file.

![blockentry](https://github.com/mbonnic-psx/local-dns-blocker/blob/master/screenshots/BlockEntry.png)

### 4) Remove Site to Host File
- This option will prompt the user to input a domain to remove.
- Once entered it will apply to the Host file.

![removeentry](https://github.com/mbonnic-psx/local-dns-blocker/blob/master/screenshots/RemoveEntry.png)

