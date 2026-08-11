# Déploiement — VPS OVH + domaine Namecheap

Procédure complète, du VPS nu jusqu'à HTTPS. Écrite pour Ubuntu 26.04 LTS avec
l'utilisateur `ubuntu`, sur un VPS où rien n'est encore installé.

**Convention :** `[VPS]` = à exécuter dans la session SSH. `[PC]` = sur ta machine Windows.

**Valeurs à remplacer partout :**

| Placeholder | Valeur |
|---|---|
| `IP_VPS` | l'IPv4 du VPS (espace client OVH) |
| `DOMAINE` | le domaine ou sous-domaine choisi |
| `8081` | port local de l'API — libre, ne pas exposer publiquement |

---

## Étape 1 — Sécuriser l'accès SSH

### 1.1 Vérifier l'état actuel

```bash
ls -la ~/.ssh/ 2>/dev/null; echo "--- CONF ---"; sudo grep -rhE "^\s*(PasswordAuthentication|PermitRootLogin|PubkeyAuthentication)" /etc/ssh/sshd_config /etc/ssh/sshd_config.d/ 2>/dev/null
```

Si `~/.ssh/authorized_keys` existe, OVH a déjà installé une clé. Sinon, faire 1.2.

### 1.2 Créer et installer une clé (seulement si absente)

`[PC]` :

```bash
ssh-keygen -t ed25519 -C "marie@italian-app"
```

`[PC]` — afficher la clé publique :

```bash
cat ~/.ssh/id_ed25519.pub
```

`[VPS]` — l'installer (remplacer par la ligne copiée) :

```bash
mkdir -p ~/.ssh && echo "COLLE_TA_CLE_ICI" >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys
```

### 1.3 Test bloquant

`[PC]`, dans un **nouveau** terminal, **sans fermer la session en cours** :

```bash
ssh ubuntu@IP_VPS
```

Doit passer sans mot de passe. **Si ça échoue, ne pas continuer** — l'étape suivante
supprimerait le seul accès restant.

### 1.4 Durcir la configuration

```bash
printf 'PermitRootLogin no\nPasswordAuthentication no\nPubkeyAuthentication yes\nKbdInteractiveAuthentication no\n' | sudo tee /etc/ssh/sshd_config.d/99-hardening.conf
```

Le `&&` empêche le redémarrage si la syntaxe est invalide :

```bash
sudo sshd -t && sudo systemctl restart ssh
```

### 1.5 Fail2ban

```bash
sudo apt update && sudo apt install -y fail2ban && sudo systemctl enable --now fail2ban
```

---

## Étape 2 — Pare-feu

Seul SSH écoute actuellement, donc aucun service à préserver. SSH d'abord, toujours.

```bash
sudo ufw allow OpenSSH && sudo ufw allow 80/tcp comment 'HTTP' && sudo ufw allow 443/tcp comment 'HTTPS'
```

```bash
sudo ufw enable
```

```bash
sudo ufw status verbose
```

> Docker contourne UFW pour les ports publiés sur `0.0.0.0`. `docker-compose.prod.yml`
> lie l'API à `127.0.0.1` : elle reste injoignable de l'extérieur quoi qu'il arrive.

---

## Étape 3 — Docker

Ubuntu 26.04 fournit Docker 29.x dans ses propres dépôts, maintenu pendant toute la LTS.

### 3.1 Vérifier la disponibilité

```bash
apt-cache policy docker.io docker-compose-v2 docker-buildx
```

Chaque paquet doit afficher un `Candidate:` avec un numéro de version. Si l'un affiche
`(none)`, passer à 3.4.

### 3.2 Installer

```bash
sudo apt install -y docker.io docker-compose-v2 docker-buildx
```

```bash
sudo usermod -aG docker $USER && sudo systemctl enable --now docker
```

### 3.3 Se reconnecter puis vérifier

`exit`, puis `ssh ubuntu@IP_VPS` — le changement de groupe n'est effectif qu'à la
nouvelle session.

```bash
docker run --rm hello-world && docker compose version
```

### 3.4 Repli : dépôt officiel Docker

Uniquement si 3.1 a échoué.

```bash
sudo apt install -y ca-certificates curl && sudo install -m 0755 -d /etc/apt/keyrings && sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc && sudo chmod a+r /etc/apt/keyrings/docker.asc
```

```bash
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
```

```bash
sudo apt update && sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

Si `apt update` renvoie un 404, Docker ne publie pas encore pour 26.04 : remplacer
`$(. /etc/os-release && echo $VERSION_CODENAME)` par `noble` (24.04, compatible), puis
relancer.

---

## Étape 4 — DNS chez Namecheap

À lancer tôt : la propagation prend de 10 minutes à quelques heures, et le certificat
SSL (étape 8) ne peut pas être émis avant.

1. namecheap.com → **Domain List** → **Manage** → onglet **Advanced DNS**
2. Supprimer les enregistrements de parking par défaut (`CNAME` vers
   `parkingpage.namecheap.com`, `URL Redirect`)
3. **Add New Record** :

| Type | Host | Value | TTL |
|---|---|---|---|
| A Record | `@` | `IP_VPS` | Automatic |
| A Record | `www` | `IP_VPS` | Automatic |

Pour un sous-domaine dédié (recommandé si le domaine sert déjà à autre chose) : une seule
ligne, Host = `italien`, ce qui donne `italien.mondomaine.com`.

4. Valider chaque ligne (coche verte à droite)

`[PC]` — vérifier la propagation :

```bash
nslookup DOMAINE
```

Doit renvoyer `IP_VPS`.

---

## Étape 5 — Pousser les fichiers de déploiement

Quatre fichiers ont été ajoutés au dépôt : `Dockerfile`, `.dockerignore`,
`docker-compose.prod.yml`, `deploy/nginx/italian-app.conf`.

`[PC]` :

```bash
git add Dockerfile .dockerignore docker-compose.prod.yml deploy/ && git commit -m "chore(deploy): dockerfile, compose de production, vhost nginx" && git push
```

---

## Étape 6 — Premier déploiement manuel

Objectif : faire répondre l'application **en local sur le VPS** avant d'ajouter nginx
puis TLS. Un problème à la fois.

### 6.1 Créer le dossier

```bash
sudo mkdir -p /srv/italian-app && sudo chown $USER:$USER /srv/italian-app
```

### 6.2 Cloner

Dépôt **public** :

```bash
git clone https://github.com/COMPTE/italian-app.git /srv/italian-app
```

Dépôt **privé** — générer une clé de déploiement :

```bash
ssh-keygen -t ed25519 -f ~/.ssh/github_deploy -N "" -C "vps-italian-app" && cat ~/.ssh/github_deploy.pub
```

Copier la clé affichée, puis sur GitHub : dépôt → **Settings** → **Deploy keys** →
**Add deploy key** → coller, laisser « Allow write access » décoché.

```bash
printf 'Host github.com\n  IdentityFile ~/.ssh/github_deploy\n  IdentitiesOnly yes\n' >> ~/.ssh/config
```

```bash
git clone git@github.com:COMPTE/italian-app.git /srv/italian-app
```

### 6.3 Générer les secrets

```bash
echo "JWT: $(openssl rand -base64 48)"; echo "PG : $(openssl rand -base64 24 | tr -d '/+=')"
```

Garder les deux valeurs sous la main.

### 6.4 Écrire le fichier `.env`

```bash
nano /srv/italian-app/.env
```

Contenu, valeurs à remplacer :

```
POSTGRES_PASSWORD=LE_PG
API_PORT=8081

JWT_SIGNING_SECRET=LE_JWT
JWT_ISSUER=italian-app
JWT_AUDIENCE=italian-app
JWT_LIFETIME_HOURS=72

IDENTITY_ALLOW_REGISTRATION=true

AZURE_SPEECH_SUBSCRIPTION=
AZURE_SPEECH_REGION=westeurope
AZURE_SPEECH_ITALIAN_VOICE=it-IT-ElsaNeural

ANTHROPIC_TOKEN=
ANTHROPIC_MODEL=claude-sonnet-5

QUOTA_SCORING_CALLS_PER_DAY=150
QUOTA_LLM_CALLS_PER_DAY=100
QUOTA_TTS_CALLS_PER_DAY=0
```

Sauvegarder : `Ctrl+O`, `Entrée`, `Ctrl+X`.

Pas de `DB_CONNECTION` ici : `docker-compose.prod.yml` la construit à partir de
`POSTGRES_PASSWORD`, avec `Host=db` (nom du service Docker, pas `localhost`) et le port
`5432` (le `5434` du compose de développement est un port côté machine hôte).

Une valeur `DB_CONNECTION` laissée dans le fichier serait ignorée — `environment` prime
sur `env_file`.

Verrouiller le fichier :

```bash
chmod 600 /srv/italian-app/.env
```

### 6.5 Construire et lancer

```bash
cd /srv/italian-app && mkdir -p audio && docker compose -f docker-compose.prod.yml build
```

La première construction télécharge le SDK .NET : quelques minutes.

```bash
docker compose -f docker-compose.prod.yml up -d
```

```bash
docker compose -f docker-compose.prod.yml ps
```

Les migrations EF tournent au démarrage (`InitialiseDatabaseAsync`) :

```bash
docker compose -f docker-compose.prod.yml logs api --tail=50
```

### 6.6 Le test qui compte

```bash
curl http://127.0.0.1:8081/health
```

Attendu : `{"status":"ok"}`. Si ce n'est pas le cas, ne pas passer à l'étape 7.

---

## Étape 7 — Nginx en frontal

Nginx écoute sur 80/443 et route selon le nom de domaine demandé — c'est ce qui permet
d'héberger plusieurs projets sur un seul VPS, un fichier de conf par domaine.

Front et API partagent une seule origine, séparés par le chemin :

```
DOMAINE/          → build React, servi depuis /srv/italian-app/web
DOMAINE/api/...   → API .NET (127.0.0.1:8081)
DOMAINE/audio/... → MP3s modèles
```

Pas de CORS, une seule autorisation micro, un seul certificat.

```bash
sudo apt install -y nginx
```

Le dossier du front doit exister, même vide tant que `src/Web/` n'est pas écrit :

```bash
sudo mkdir -p /srv/italian-app/web && sudo chown $USER:$USER /srv/italian-app/web
```

```bash
sudo cp /srv/italian-app/deploy/nginx/italian-app.conf /etc/nginx/sites-available/italian-app
```

Remplacer les placeholders (adapter domaine et port) :

```bash
sudo sed -i 's/DOMAIN/DOMAINE www.DOMAINE/; s/API_PORT/8081/' /etc/nginx/sites-available/italian-app
```

```bash
sudo ln -s /etc/nginx/sites-available/italian-app /etc/nginx/sites-enabled/italian-app
```

Tester la syntaxe **avant** de recharger :

```bash
sudo nginx -t
```

`reload` ne coupe pas les connexions en cours, contrairement à `restart` :

```bash
sudo systemctl reload nginx
```

Vérification `[PC]` : `http://DOMAINE/health` renvoie `{"status":"ok"}`. En clair, c'est
normal à ce stade. La racine `/` renvoie 404 tant que le front n'est pas déployé.

### Déployer le front (quand `src/Web/` existera)

Le front appelle l'API en **relatif** (`/api/auth/login`) : aucune URL à configurer, aucune
variable d'environnement, le build est le même en local et en production.

`[PC]` — pendant le développement, `vite.config.ts` renvoie `/api` vers l'API locale :

```js
server: { proxy: { '/api': 'http://localhost:5043', '/audio': 'http://localhost:5043' } }
```

Déploiement : `npm run build` produit `dist/`, dont le contenu est copié dans
`/srv/italian-app/web`. C'est GitHub Actions qui le fera.

---

## Étape 8 — HTTPS (Let's Encrypt)

HTTPS n'est pas optionnel ici : les navigateurs refusent l'accès au micro sur une origine
non sécurisée. Sans TLS, l'application ne peut pas fonctionner.

```bash
sudo apt install -y certbot python3-certbot-nginx
```

Certbot lit la conf nginx, prouve le contrôle du domaine, puis **réécrit le fichier**
pour ajouter le bloc 443 et la redirection HTTP → HTTPS :

```bash
sudo certbot --nginx -d DOMAINE -d www.DOMAINE
```

Questions posées : email (alertes d'expiration — en mettre un vrai), acceptation des
conditions (`A`), newsletter (`N`).

> Le DNS doit être propagé **avant**. Let's Encrypt limite à 5 échecs de validation par
> heure et par domaine.

Vérifier le renouvellement automatique (certificat valable 90 jours) :

```bash
sudo certbot renew --dry-run
```

```bash
systemctl list-timers | grep certbot
```

Vérification finale : `https://DOMAINE/health` avec cadenas fermé, et `http://DOMAINE`
qui redirige vers `https://`.

---

## Étape 9 — Fermer l'inscription

Une fois le compte créé via l'endpoint d'inscription :

```bash
cd /srv/italian-app && sed -i 's/IDENTITY_ALLOW_REGISTRATION=true/IDENTITY_ALLOW_REGISTRATION=false/' .env && docker compose -f docker-compose.prod.yml up -d --force-recreate api
```

---

## Architecture obtenue

```
Internet ──► :443 nginx (TLS, un vhost par domaine)
                 │
                 └─► DOMAINE ──► 127.0.0.1:8081 ──► italian-app-api
                                                          │ réseau Docker privé
                                                          └─► italian-app-db
```

La base n'est jamais exposée à l'hôte. L'API n'est joignable que depuis le VPS lui-même.
Chaque projet futur ajoute son propre vhost, son port local et son réseau Docker.

---

## Commandes du quotidien

Déployer une nouvelle version :

```bash
cd /srv/italian-app && git pull && docker compose -f docker-compose.prod.yml up -d --build
```

Voir les logs en direct :

```bash
cd /srv/italian-app && docker compose -f docker-compose.prod.yml logs -f api
```

Sauvegarder la base :

```bash
docker exec italian-app-db pg_dump -U italianapp italianapp | gzip > ~/backup-$(date +%F).sql.gz
```

Redémarrer l'API seule :

```bash
docker compose -f docker-compose.prod.yml restart api
```

---

## En cas de problème

| Symptôme | Diagnostic |
|---|---|
| `curl 127.0.0.1:8081/health` échoue | `docker compose -f docker-compose.prod.yml logs api` |
| L'API démarre puis s'arrête | mot de passe Postgres incohérent entre `DB_CONNECTION` et `POSTGRES_PASSWORD` |
| nginx renvoie 502 | l'API ne tourne pas, ou `API_PORT` ≠ port dans le vhost |
| certbot échoue | DNS non propagé — vérifier `nslookup DOMAINE` |
| `docker` demande sudo | déconnexion/reconnexion manquante après `usermod -aG docker` |

---

## Étape 10 — Déploiement continu (GitHub Actions)

Chaque push sur `main` déclenche : tests → construction de l'image → publication sur GHCR
→ connexion SSH au VPS qui récupère l'image et redémarre. Le VPS ne compile plus rien.

### 10.1 Clé SSH dédiée au déploiement

Une clé distincte de la tienne, révocable seule.

```bash
ssh-keygen -t ed25519 -f ~/.ssh/github_actions -N "" -C "github-actions-deploy"
```

```bash
cat ~/.ssh/github_actions.pub >> ~/.ssh/authorized_keys
```

```bash
cat ~/.ssh/github_actions
```

Copier **tout** l'affichage, lignes `-----BEGIN` et `-----END` comprises.

### 10.2 Secrets GitHub

Dépôt → **Settings** → **Secrets and variables** → **Actions** → **New repository secret** :

| Nom | Valeur |
|---|---|
| `VPS_HOST` | `pianopiano.site` |
| `VPS_USER` | `ubuntu` |
| `VPS_SSH_KEY` | la clé privée copiée en 10.1 |

### 10.3 Pointer le compose sur l'image du registre

Dans `/srv/italian-app/.env`, ajouter (nom du dépôt **en minuscules**) :

```
API_IMAGE=ghcr.io/COMPTE/italian-app:latest
```

### 10.4 Rendre le paquet GHCR public

Après le premier passage du workflow : profil GitHub → **Packages** → `italian-app` →
**Package settings** → **Change visibility** → **Public**.

Sans ça, le VPS devrait s'authentifier pour récupérer l'image. Le dépôt est déjà public et
l'image ne contient aucun secret — ils viennent tous du `.env`, au runtime.

### 10.5 Vérifier

Onglet **Actions** du dépôt après un push sur `main`. Le job `Production` échoue
volontairement si `/health` ne répond pas dans les 60 s, et affiche alors les logs.

Retour arrière vers une version précédente — chaque commit est taggé `sha-xxxxxxx` :

```bash
cd /srv/italian-app && API_IMAGE=ghcr.io/COMPTE/italian-app:sha-XXXXXXX docker compose -f docker-compose.prod.yml up -d --no-build
```

---

## pgAdmin

### Développement

`docker-compose.yml` inclut pgAdmin, en mode bureau (ni login ni mot de passe maître).

`[PC]` :

```bash
docker compose up -d
```

Interface sur <http://localhost:5050>. Ajouter le serveur : hôte `db`, port `5432`,
base/utilisateur/mot de passe `italianapp`.

### Production

Aucun pgAdmin n'est installé sur le VPS : une interface d'administration de base exposée
sur Internet est une surface d'attaque permanente, et elle consommerait de la RAM en
continu pour un usage occasionnel.

La base est publiée sur `127.0.0.1:5433` du VPS — inaccessible de l'extérieur. On y accède
par un tunnel SSH, depuis le pgAdmin qui tourne déjà en local.

`[PC]` — ouvrir le tunnel, et le laisser tourner :

```bash
ssh -N -L 5433:127.0.0.1:5433 ubuntu@pianopiano.site
```

Puis dans pgAdmin, ajouter un second serveur nommé « Production » :

| Champ | Valeur |
|---|---|
| Host | `host.docker.internal` |
| Port | `5433` |
| Database | `italianapp` |
| Username | `italianapp` |
| Password | valeur de `POSTGRES_PASSWORD` du `.env` du VPS |

`host.docker.internal` et non `localhost` : pgAdmin tourne dans un conteneur, `localhost`
y désigne le conteneur lui-même, pas ta machine Windows où débouche le tunnel.

Fermer le tunnel (`Ctrl+C`) referme l'accès.

---

## Reste à faire

- `ForwardedHeaders` dans `Program.cs` : nginx envoie `X-Forwarded-Proto`, l'API ne le lit
  pas encore et se croit en HTTP
- Front React (`src/Web/`) : sera servi en statique par le même vhost
- .NET 8 : fin de support en novembre 2026
