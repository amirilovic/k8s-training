# Kubernetes training: laptop setup

## What you need


| Software               | Version | What it is for                                                         |
| ---------------------- | ------- | ---------------------------------------------------------------------- |
| WSL2 with Ubuntu 26.04 | latest  | The Linux environment where all exercises run                          |
| Docker Engine          | latest  | Runs containers inside Ubuntu (free, no Docker Desktop licence needed) |
| kubectl                | v1.37.1 | The Kubernetes command-line client                                     |
| kind                   | v0.33.0 | Runs a local Kubernetes cluster inside Docker                          |
| Helm                   | v4.3.0  | Kubernetes package manager, used on day 3                              |
| k9s                    | v0.51.0 | Terminal UI for Kubernetes, optional but recommended                   |
| Git                    | latest  | To clone the exercise repository                                       |
| VS Code + extensions   | latest  | Editing YAML files inside WSL                                          |


Hardware: Windows 10 22H2 or Windows 11, 16 GB RAM recommended (8 GB is the minimum), 20 GB of free disk space, and administrator rights on the laptop.

These are the latest stable releases as of October 6, 2026. kind v0.33.0 runs Kubernetes 1.37 by default, matching kubectl. Please use the versions in the table. Everyone running the same versions means the exercises behave the same on every laptop.

## Important: run everything inside Ubuntu

From step 2 onward, every command in this document and in the training runs in the **Ubuntu terminal**, not in PowerShell or cmd. You open it from the Start menu ("Ubuntu 26.04") or as a tab in Windows Terminal.

If your prompt shows a path starting with `/mnt/c/` (for example `/mnt/c/WINDOWS/system32` after starting WSL from an admin PowerShell), you are in a Windows folder. Run `cd ~` to go to your Linux home directory before continuing.

## 1. Install WSL2 and Ubuntu

Open **PowerShell as Administrator** (right-click the Start button, then "Terminal (Admin)") and run:

```powershell
wsl --update
wsl --install -d Ubuntu-26.04
```

Do not skip `wsl --update`, even if WSL is already installed. Ubuntu 26.04 is new, and older WSL versions fail to start it with `Catastrophic failure, Error code: Wsl/Service/E_UNEXPECTED`.

Reboot when asked. After the reboot, the Ubuntu window opens and asks you to create a Linux username and password. Remember the password, `sudo` asks for it.

Check that Ubuntu runs on WSL version 2. In PowerShell:

```powershell
wsl -l -v
```

The `VERSION` column for `Ubuntu-26.04` must show `2`. If it shows `1`, run `wsl --set-version Ubuntu-26.04 2`.

### If you already have other WSL distributions

The list may show more than one distribution, for example:

```
  NAME            STATE           VERSION
* Ubuntu          Running         2
  Ubuntu-26.04    Stopped         2
```

The `*` marks the default. Typing `wsl` in PowerShell, and tools like VS Code that look for "the" WSL distribution, use the default, which here is the old `Ubuntu`, not the one for the training. The Start menu and Windows Terminal also list both, as "Ubuntu" and "Ubuntu 26.04", and they are easy to mix up. Make `Ubuntu-26.04` the default:

```powershell
wsl --set-default Ubuntu-26.04
```

Run `wsl -l -v` again and check that the `*` is now next to `Ubuntu-26.04`. To be sure you are in the right one at any time, run `cat /etc/os-release | grep VERSION=` inside the terminal; it must say `26.04`.

You can keep your other distributions. If one of them runs Docker (its own Docker Engine or Docker Desktop's integration), stop it during the training with `wsl --terminate <name>`. All WSL2 distributions share one network, so two Docker daemons can fight over the same ports.

### If something fails

- **Error about virtualization** (for example `0x80370102`): virtualization is turned off in the BIOS. Enable "Intel VT-x" or "AMD-V/SVM" in the BIOS settings and try again.
- **`Catastrophic failure` with `Wsl/Service/E_UNEXPECTED`**: WSL is too old. Run `wsl --update` in PowerShell as Administrator, then `wsl --shutdown`, then start Ubuntu 26.04 again. Your files inside Ubuntu are not affected.

## 2. Update Ubuntu and check systemd

In the Ubuntu terminal:

```bash
sudo apt-get update && sudo apt-get upgrade -y
sudo apt-get install -y git curl ca-certificates
```

Docker needs systemd. Check it is enabled:

```bash
cat /etc/wsl.conf
```

The output must contain:

```
[boot]
systemd=true
```

**If it does not, add it:**

```bash
echo -e "[boot]\nsystemd=true" | sudo tee -a /etc/wsl.conf
```

Then run `wsl --shutdown` in PowerShell and open Ubuntu again.

## 3. Install Docker Engine

If you already have **Docker Desktop** installed, open its settings, go to Resources, then WSL integration, and turn off the integration for Ubuntu-26.04. Otherwise Docker Desktop and Docker Engine conflict inside Ubuntu.

In the Ubuntu terminal:

```bash
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin
sudo usermod -aG docker $USER
sudo systemctl enable --now docker
```

**Close the Ubuntu terminal**, run `wsl --shutdown` in PowerShell, and open Ubuntu again so your user picks up the `docker` group. Then check:

```bash
docker run --rm hello-world
```

You should see "Hello from Docker!". If you get `permission denied ... docker.sock`, run `groups $USER` (with `$USER`; plain `groups` only shows the groups of the current shell and will not list `docker` until you restart). If `docker` is missing from that list, run `sudo usermod -aG docker $USER` again. Either way, close the Ubuntu window, run `wsl --shutdown` in PowerShell, and open Ubuntu 26.04 again. Closing the window alone is not enough, because WSL keeps running in the background.

## 4. Install kubectl

```bash
ARCH=$(dpkg --print-architecture)
curl -LO "https://dl.k8s.io/release/v1.37.1/bin/linux/${ARCH}/kubectl"
sudo install -m 0755 kubectl /usr/local/bin/kubectl
rm kubectl
kubectl version --client
```

## 5. Install kind

```bash
ARCH=$(dpkg --print-architecture)
curl -Lo kind "https://kind.sigs.k8s.io/dl/v0.33.0/kind-linux-${ARCH}"
sudo install -m 0755 kind /usr/local/bin/kind
rm kind
kind version
```

## 6. Install Helm

```bash
ARCH=$(dpkg --print-architecture)
curl -L "https://get.helm.sh/helm-v4.3.0-linux-${ARCH}.tar.gz" | tar xz
sudo install -m 0755 "linux-${ARCH}/helm" /usr/local/bin/helm
rm -rf "linux-${ARCH}"
helm version
```

## 7. Install k9s

```bash
ARCH=$(dpkg --print-architecture)
curl -Lo k9s.deb "https://github.com/derailed/k9s/releases/download/v0.51.0/k9s_linux_${ARCH}.deb"
sudo apt-get install -y ./k9s.deb
rm k9s.deb
k9s version
```

## 8. Install VS Code and extensions

1. Install [VS Code](https://code.visualstudio.com/) on Windows (the normal Windows installer).
2. In VS Code, install the **WSL** extension (`ms-vscode-remote.remote-wsl`).
3. In the Ubuntu terminal, run `code .`. The first time, this installs the VS Code server inside Ubuntu and opens a VS Code window connected to WSL (bottom-left corner shows "WSL: Ubuntu-26.04").
4. In that WSL window, install two more extensions: **Kubernetes** (`ms-kubernetes-tools.vscode-kubernetes-tools`) and **YAML** (`redhat.vscode-yaml`).

**Optional**, if your laptop has 16 GB RAM or less: limit how much memory WSL may take. Create the file `C:\Users\<your-windows-user>\.wslconfig` with:

```ini
[wsl2]
memory=8GB
```

and run `wsl --shutdown` in PowerShell.

## 9. Final check

Clone the training repository into your Linux home directory and run the preflight check. It creates a small Kubernetes cluster, runs the training app in it, calls it on `http://localhost:30080`, and deletes the cluster again. It takes 2 to 5 minutes, most of it downloading images the first time.

```bash
cd ~
git clone https://github.com/amirilovic/k8s-training.git
cd k8s-training
setup/preflight.sh
```

The check is successful when the last line says `All good. You are ready for Monday.`

**Send me the complete output**, whether it worked or not. If something failed, we will fix it in a short call before Monday.

## Before Monday

- Do not leave a cluster running. We create a fresh one together on Monday morning - the last script creates and deletes the cluster, if that was successful, nothing else to do.