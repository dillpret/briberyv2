# Hosting Bribery on an Oracle Cloud Free Tier Compute Instance

The following guide walks through provisioning an Oracle Cloud Infrastructure (OCI) Free Tier VM, preparing it to run the Bribery backend (ASP.NET Core API) and frontend (Angular), and wiring up the automated deployment pipeline that lives in `.github/workflows/deploy.yml`.

> **Assumptions**
> - You already have an Oracle Cloud account with Free Tier access.
> - A public DNS record is optional; you can operate with the public IP assigned to the instance.
> - Commands target the Oracle-provided Ubuntu 22.04 image. Adjust package commands accordingly if you choose a different OS.
> - You have push access to the GitHub repository and permission to manage Actions secrets.

## 1. Provision the compute instance

1. Sign in to the [OCI Console](https://cloud.oracle.com/).
2. Navigate to **Compute → Instances** and click **Create instance**.
3. Name the instance (e.g., `bribery-game`) and select **Always Free Eligible**.
4. Under **Image and shape**, choose the latest Ubuntu 22.04 image and the `VM.Standard.A1.Flex` shape (1 OCPU, 6GB RAM is typically available).
5. Leave the boot volume at the default size (typically 46GB) or adjust as needed.
6. Under **Networking**, either select an existing VCN/Subnet or allow OCI to create a new one. Ensure the subnet is public and assigned a public IPv4 address.
7. Upload or generate an SSH key pair. Keep the private key secure—you will use it to connect to the instance and from GitHub Actions.
8. Click **Create** and wait for the instance to reach the `RUNNING` state.

## 2. Configure network access

1. From the instance details page, note the **Public IPv4 address**.
2. Open the **Subnet** linked to the instance and edit the **Security List**:
   - Add ingress rules allowing TCP traffic on ports `22` (SSH), `80` (HTTP), and `443` (HTTPS) from your desired CIDR ranges (for public access use `0.0.0.0/0`).
   - Add an egress rule permitting outbound traffic to `0.0.0.0/0` so the server can download dependencies.
3. If you are using a network security group (NSG), mirror the same rules there.

## 3. Connect to the instance

Use SSH from your local terminal (replace the path to your private key and the public IP):

```bash
ssh -i ~/.ssh/oci-bribery.key ubuntu@<public-ip>
```

The default user for Ubuntu images is `ubuntu`.

## 4. Install runtime dependencies

Update the package index and install prerequisites (the deployment workflow relies on `rsync` to sync files onto the server):

```bash
sudo apt-get update
sudo apt-get install -y apt-transport-https ca-certificates curl git gnupg nginx rsync
```

### Install .NET 8 SDK

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0
```

### Install Node.js 18 and Angular CLI build tooling

```bash
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt-get install -y nodejs build-essential
sudo npm install -g @angular/cli
```

(Headless Chrome dependencies for the Angular tests are optional in production. They are already bundled with Puppeteer for local development.)

## 5. Configure system services (one-time server work)

The GitHub Actions workflow will publish the API into `/var/www/bribery-api` and the Angular static assets into `/var/www/bribery-client`. Prepare those locations and the reverse proxy before enabling automated deployments.

### Backend API service

1. Create the target directories (the workflow also creates them but doing so now allows the service file to reference a stable path):
   ```bash
   sudo mkdir -p /var/www/bribery-api
   sudo mkdir -p /var/www/bribery-client
   ```
2. Create a systemd unit at `/etc/systemd/system/bribery-api.service`:
   ```ini
   [Unit]
   Description=Bribery ASP.NET Core API
   After=network.target

   [Service]
   WorkingDirectory=/var/www/bribery-api
   ExecStart=/usr/bin/dotnet /var/www/bribery-api/Bribery.Api.dll --urls http://0.0.0.0:5000
   Restart=always
   RestartSec=5
   SyslogIdentifier=bribery-api
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production

   [Install]
   WantedBy=multi-user.target
   ```
3. Reload systemd so it recognises the new service and enable it for future boots (it will be started automatically by the deployment workflow once the binaries are in place):
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable bribery-api
   ```

### Nginx reverse proxy & static hosting

1. Remove the default site and create a new server block:
   ```bash
   sudo rm /etc/nginx/sites-enabled/default
   sudo nano /etc/nginx/sites-available/bribery
   ```
2. Paste the following configuration (adjust `server_name` to your domain or public IP):
   ```nginx
   server {
       listen 80;
       server_name your-domain.example.com;

       root /var/www/bribery-client;
       index index.html;

       location / {
           try_files $uri $uri/ /index.html;
       }

       location /api/ {
           proxy_pass http://127.0.0.1:5000/api/;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection keep-alive;
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }
   }
   ```
3. Enable the site and reload Nginx:
   ```bash
   sudo ln -s /etc/nginx/sites-available/bribery /etc/nginx/sites-enabled/bribery
   sudo nginx -t
   sudo systemctl reload nginx
   ```

(Optional) Configure HTTPS with [Let’s Encrypt](https://certbot.eff.org/instructions?ws=nginx&os=ubuntufocal) once your DNS record resolves to the instance.

## 6. Configure the GitHub Actions deployment

The repository includes a GitHub Actions workflow at `.github/workflows/deploy.yml`. It restores dependencies, runs the .NET and Angular test suites, builds release artifacts, securely copies them to the server using `scp`, and then promotes them into `/var/www/bribery-api` and `/var/www/bribery-client` over SSH.

Perform the following manual steps so the workflow can reach your Oracle VM:

1. In the GitHub UI, navigate to **Settings → Secrets and variables → Actions** and create the following repository secrets:
   - `DEPLOY_HOST` – the public IP address or DNS name of your OCI instance.
   - `DEPLOY_USER` – the SSH user (e.g., `ubuntu`). This user must be able to run `sudo` for `rsync`, `chown`, and `systemctl` without an interactive password prompt.
   - `DEPLOY_SSH_KEY` – the private key that matches the public key uploaded when provisioning the instance. Paste it in PEM format.
   - `DEPLOY_SSH_PASSPHRASE` – *(optional)* passphrase for the private key above.
   - `DEPLOY_PORT` – *(optional)* SSH port if you do not use the default `22`.
2. Ensure the instance allows SSH connections from the GitHub Actions runners (they originate from GitHub’s published IP ranges). If necessary, widen your security list to `0.0.0.0/0` for port `22`.
3. If you need to use different deployment directories or a different service name, edit the environment variables at the top of `.github/workflows/deploy.yml` before committing. By default it targets `/var/www/bribery-api`, `/var/www/bribery-client`, restarts the `bribery-api` systemd unit, and reloads `nginx` after each deployment.

## 7. Trigger the deployment pipeline

1. Push to the `main` branch (or merge a pull request into `main`). This automatically runs the **Build and Deploy Bribery** workflow.
2. To deploy on demand without a new commit, go to **Actions → Build and Deploy Bribery → Run workflow** and select the branch you want to deploy.
3. Monitor the workflow logs to confirm that the build, tests, artifact upload, and remote deployment all succeed. Any SSH or service configuration issues will surface here.

## 8. Verify the deployment

1. Visit `http://<public-ip>/` (or your domain) in a browser. You should see the Bribery landing page.
2. Create a lobby and ensure API calls succeed. If requests fail, check:
   - The GitHub Actions workflow logs for deployment errors.
   - `sudo journalctl -u bribery-api -f` for backend logs on the instance.
   - `sudo tail -f /var/log/nginx/error.log` for proxy issues.
3. To roll out future changes, simply merge to `main` or manually dispatch the workflow. It will redeploy the latest code, run the tests, and recycle the services automatically.

The Bribery game is now available to your players via the Oracle Cloud Free Tier instance with continuous delivery powered by GitHub Actions.
