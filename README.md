# Italian App — entraînement à l'italien oral

Application web de **drill de prononciation** italienne pour une apprenante francophone.
Une mise en situation est affichée, la phrase cible est présentée en italien et en français,
un audio modèle est joué, l'utilisatrice répète, un score de prononciation est calculé
et un conseil rédigé en français lui est restitué.

Le jeu de rôle conversationnel libre est hors périmètre V1.

## Stack

| Couche | Technologie |
|---|---|
| Front | React + Vite, PWA (cible tablette) |
| API | .NET 8, Minimal API |
| Base | PostgreSQL 16 |
| Scoring | Azure Speech — Pronunciation Assessment |
| TTS | Azure Speech — Neural TTS (hors runtime) |
| Feedback rédigé | Claude API |
| Audio modèles | Fichiers statiques |

## Démarrage

```bash
cp .env.example .env   # puis renseigner les clés
docker compose up -d db
dotnet run --project src/Api
```

L'API écoute sur `https://localhost:7043` (Swagger sur `/swagger` en développement).
**HTTPS est obligatoire** : les navigateurs refusent l'accès micro sur origine non sécurisée.

Tests :

```bash
dotnet test
```

Les tests d'intégration démarrent un PostgreSQL jetable via Testcontainers — Docker doit
tourner, mais aucune clé d'API n'est nécessaire.

## Compte

L'inscription est ouverte par défaut pour créer le compte initial. Une fois celui-ci créé,
passer `IDENTITY_ALLOW_REGISTRATION=false` : l'application n'a qu'une utilisatrice, et une
inscription ouverte sur une URL publique n'a aucune raison de le rester.

---

## Trois décisions d'architecture

Ces trois points structurent le reste du code et ne sont pas des détails d'implémentation.

### 1. Le scoring de prononciation n'est pas fait par un LLM

Un LLM ne traite pas le signal audio. Un moteur de transcription (type Whisper) est encore pire
pour cet usage : il *corrige* les erreurs de prononciation au lieu de les révéler — un « pé-nné »
mal articulé ressort transcrit `penne`, propre et faux. Faire commenter cette transcription par un
LLM produirait un feedback plausible mais sans rapport avec ce qui a réellement été prononcé.

Le scoring est donc délégué à **Azure Speech — Pronunciation Assessment**, qui rend un score
par phonème plus des scores de fluidité, complétude et accentuation.

Le LLM intervient **uniquement en aval**, pour transformer des scores bruts et des conseils
déjà rédigés en un texte lisible. Il ne juge jamais la prononciation.

### 2. Les audios modèles sont pré-générés, jamais synthétisés au runtime

Le catalogue est figé après relecture. Chaque phrase reçoit son MP3 une seule fois via
`seed-audio`, commande idempotente. Le quota TTS runtime est fixé à **0 par conception**.

Motivation : préserver le palier gratuit, et surtout garantir une latence nulle sur la boucle
« écouter → répéter → réécouter », qui est le cœur de l'usage.

### 3. L'audio des tentatives n'est jamais persisté

Le flux capté par le micro transite par l'API, est relayé vers Azure, et le buffer est libéré
immédiatement après réception du score. **Aucun enregistrement n'est écrit sur disque ni en base.**
Seuls les scores numériques et le texte du feedback sont conservés.

C'est un choix RGPD assumé : la voix est une donnée biométrique, et l'application n'a aucun
besoin fonctionnel de la conserver — la progression se mesure sur les scores.

---

## Pipeline de contenu

Le contenu n'est pas écrit à la main phrase par phrase, mais il n'est pas non plus importé
tel que généré.

```
seed-content  →  content/generated/*.json  →  RELECTURE HUMAINE  →  content/reviewed/*.json  →  import
```

1. **`seed-content`** appelle Claude avec un prompt de contexte par catégorie (situation, registre,
   niveau visé) et écrit un JSON de phrases candidates : italien, traduction, mise en situation,
   annotation des pièges phonétiques.
2. **La relecture manuelle n'est pas optionnelle.** L'utilisatrice est A2 : elle n'a aucun moyen de
   détecter une tournure bancale, un registre inadapté ou une annotation de piège erronée. Une erreur
   non détectée serait apprise.
3. **L'import** définit `reviewedAt`. Une phrase sans `reviewedAt` n'est **jamais servie**.
4. **`seed-audio`** synthétise les MP3 manquants (`audioUrl IS NULL`) et renseigne `audioUrl`
   et `ttsVoice`. Relançable après chaque ajout sans regénérer l'existant.

Volume cible V1 : 4 catégories × 4 scénarios × 15 phrases ≈ 240 phrases.

## Quotas

Compteurs journaliers par utilisateur, configurables par variables d'environnement.

| Compteur | Défaut / jour | Comportement au dépassement |
|---|---|---|
| Scoring | 150 | Session bloquée, message explicite, réinitialisation à minuit |
| LLM | 100 | Bascule **silencieuse** sur `RuleBasedFeedbackWriter` |
| TTS runtime | 0 | Interdit par conception |

Le dépassement du quota LLM n'interrompt jamais l'entraînement : seul le style du feedback change.

## Structure

```
src/Api/Features/       vertical slices : Identity, Catalog, Practice, Progress, Quota
src/Api/Infrastructure/ Speech, Llm, Persistence
src/Cli/                seed-content, seed-audio
src/Web/                front React
tests/                  unitaires + intégration (implémentations Fake uniquement)
```

Le découpage est en **vertical slices** par fonctionnalité, pas en couches horizontales :
à cette taille, Domain/Application/Infrastructure produit surtout de la cérémonie.

Aucun appel API réel n'est effectué en CI : les implémentations `Fake*` sont sélectionnées
en l'absence de clés.
