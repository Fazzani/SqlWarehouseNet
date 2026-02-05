# Plan Technique : Améliorations SqlWarehouse-net

## Architecture

Le code sera organisé pour séparer les responsabilités tout en restant dans `Program.cs` pour l'instant (ou déplacé vers des classes dédiées si la taille devient critique).

## Étapes d'Implémentation

### Étape 1 : Persistance et Historique

- [x] Créer une classe statique `HistoryManager` pour gérer les opérations I/O sur `.sqlwarehouse_history`. (Implémenté directement dans `Program.cs` pour l'instant)
- [x] Modifier `ReadLineWithHistory` pour utiliser cette source de données.

### Étape 2 : Amélioration de l'UX (Progress Bars)

- [x] Remplacer les logs `AnsiConsole.MarkupLine` par des tâches de progression `AnsiConsole.Progress()`.
- [x] Paralléliser le téléchargement des chunks Arrow si possible (optimisation).

### Étape 3 : Commandes d'Export

- [x] Créer un service d'exportation capable de convertir une `List<RecordBatch>` (Arrow) en texte (CSV/JSON).
- [x] Ajouter un gestionnaire de commandes (`/export`, `/help`, `/q`) avant l'exécution SQL.

### Étape 4 : Mode Multi-ligne

- [x] Ajuster l'intercepteur de touches pour ne déclencher l'exécution via `Ctrl+Enter`.

### Étape 5 : Gestion de Profils

- [x] Implémenter une classe `ProfileManager` pour charger les configurations de profils à partir d'un fichier JSON ou de variables d'environnement.
- [x] Ajouter une commande pour switcher de profil (`/profile switch [profile_name]`).

### Étape 6 : Streaming des Résultats

- [ ] Explorer la possibilité d'afficher les résultats au fur et à mesure de leur réception via l'API, en utilisant des tâches asynchrones pour le rendu.

### Étape 7 : Auto-complétion (IntelliSense light)

- [x] Charger les noms des tables et colonnes au démarrage et proposer des suggestions. (Implémenté pour les commandes CLI via `Tab`)

## Risques et Mitigations

- **Volume de l'historique** : Limiter à 500 entrées pour éviter de ralentir le chargement.
- **Précision Arrow** : S'assurer que tous les types Apache Arrow sont correctement convertis lors de l'export CSV.
