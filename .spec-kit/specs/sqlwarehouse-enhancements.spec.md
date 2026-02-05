# Spécification Fonctionnelle : Améliorations SqlWarehouse-net

## Objectif

Transformer le client CLI actuel en un outil professionnel, performant et ergonomique pour interagir avec Azure Databricks.

## Fonctionnalités Clés

### 1. Persistance de l'Historique

- **Description** : Sauvegarder les requêtes SQL saisies dans un fichier local.
- **Détails** :
  - Fichier : `.sqlwarehouse_history` à la racine de l'application.
  - Comportement : Chargement au démarrage, sauvegarde après chaque exécution réussie.
  - Navigation : Utilisation des flèches ↑/↓ pour naviguer dans l'historique persistant.

### 2. Exportation des Résultats

- **Description** : Permettre d'extraire les données affichées vers des fichiers.
- **Commandes** :
  - `/export csv [file_path]`
  - `/export json [file_path]`
- **Détails** : Utilise les données Arrow déjà en mémoire pour générer l'export sans nouvel appel API.

### 3. Expérience Utilisateur (UX) Avancée

- **Indicateurs de progression** : Utilisation de `Progress` de Spectre.Console pour le polling et le téléchargement des segments Arrow.
- **Multi-ligne** : Support des requêtes SQL sur plusieurs lignes se terminant par `;`.
- **Statut** : Affichage clair du profil actif (Dev/STG/Prod).

### 4. Gestion de Profils

- **Description** : Switcher entre différents environnements Databricks.
- **Détails** : Support d'un fichier `profiles.json` ou de variables d'environnement préfixées.

## Contraintes Techniques

- Framework : .NET 10
- Format de données : Apache Arrow (obligatoire pour la performance)
- Bibliothèque UI : Spectre.Console
- API : Databricks SQL Statement Execution API v2.0
