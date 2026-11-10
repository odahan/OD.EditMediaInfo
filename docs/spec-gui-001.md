# SPEC-GUI-001 — Interface graphique d’OD.EditMediaInfo

Décision validée le 11 novembre 2026.

## Objectif

Ajouter une interface graphique Windows à OD.EditMediaInfo dans la solution existante. L’interface graphique et la CLI s’appuieront sur le même code pour inspecter les fichiers, préparer les modifications et enregistrer les métadonnées.

## Choix validés

- **WPF sur .NET 10**, avec **MVVM Toolkit** pour les modèles de vue et les commandes.
- **Thème sombre** pour l’ensemble de l’interface, y compris la grille et les dialogues propres à l’application. Les libellés, messages et actions de l’interface sont **en anglais**.
- **`DataGrid` WPF intégré**, sans contrôles tiers. La grille est le composant central de consultation et d’édition. La fidélité visuelle à Windows 11 est secondaire par rapport à la lisibilité et au comportement de la grille.
- **Deux exécutables autonomes Windows x64**, `odmedia.exe` pour la CLI et le terminal interactif existants, et `odmedia-gui.exe` pour WPF. Ils sont publiés dans le même dossier de distribution portable.

## Étape 1 — Extraire le code partagé

1. Ajouter une bibliothèque `OD.EditMediaInfo.Core` dans la solution. Y regrouper les modèles, les services d’inspection et d’écriture, le catalogue des champs, les services de presets et l’état d’édition réutilisable (`EditorViewModel` et `EditorField`).
2. Conserver dans le projet console l’analyse des arguments, les interactions clavier et le rendu Spectre.Console. Isoler les fonctions utilisées par l’état d’édition, telles que le formatage des tailles, du rendu terminal.
3. Faire référencer la bibliothèque par la CLI et adapter les références des tests. Vérifier les commandes et le mode interactif existants avant d’ajouter la GUI. Conserver les règles actuelles de validation, de lecture seule et de sauvegarde, y compris la vérification des modifications sur une copie temporaire avant le remplacement de l’original et la conservation de la première sauvegarde `.bak` par défaut.

## Étape 2 — Ajouter la façade WPF

Ajouter un projet WPF à la solution qui référence `OD.EditMediaInfo.Core`. Le premier jalon couvre le parcours suivant.

1. Ouvrir un fichier via `Open…` ou par glisser-déposer.
2. Consulter les sections disponibles pour le média et rechercher des champs. La grille distingue les valeurs enregistrées, les modifications en attente et les champs en lecture seule.
3. Modifier, supprimer explicitement ou rétablir un champ pris en charge en passant par l’état d’édition partagé. Une saisie vide ne déclenche pas de suppression.
4. Relire les valeurs avant et après modification, puis enregistrer via le service partagé. En cas d’échec, afficher le problème et conserver les modifications en attente. À l’ouverture d’un autre fichier ou à la fermeture de la fenêtre, proposer `Save`, `Discard` ou `Cancel` lorsque des changements sont en attente.

L’inspection et la sauvegarde s’exécutent en arrière-plan pour garder la fenêtre réactive. Les écrans de gestion des presets et des pochettes pourront suivre après validation de ce parcours.

## Distribution et vérification

- Publier `odmedia.exe` et `odmedia-gui.exe` dans un même dossier. Les deux exécutables utilisent le même sous-dossier `presets` et la même installation facultative de `ffprobe.exe` et `ffmpeg.exe` placée à côté d’eux. Conserver les mécanismes de recherche existants, notamment `ODMEDIA_PRESET_DIR` et `ODMEDIA_FFPROBE`.
- Vérifier les chemins à partir des exécutables publiés, puis recommencer après avoir déplacé le dossier complet. Confirmer qu’un preset créé depuis la CLI est accessible via le service utilisé par la GUI et que les deux exécutables localisent le même analyseur.
- Valider le parcours d’ouverture, d’inspection, de modification et d’enregistrement sur un fichier éditable. Vérifier aussi l’affichage d’un fichier en lecture seule et la conservation des modifications en attente après un échec de sauvegarde.
- Exécuter les tests existants et contrôler le lancement des deux exécutables publiés. Passer explicitement `-m:1` à chaque commande `dotnet build` et `dotnet test`.
