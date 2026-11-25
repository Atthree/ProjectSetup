# Changelog

## [0.1.0] - 2026-11-25
- Initial version: Project Setup window (Window > Atthree > Project Setup) with Project, Folders,
  Packages, Settings and Git & Scenes tabs, and **Run Setup** for all steps.
- Project info: company/product name, application id for Standalone/Android/iOS, root namespace.
- Folders under a root folder (`_Project`), safe paths only, `.gitkeep` in empty folders.
- Package catalog: 8 Atthree git packages (tagged or newest) and 10 Unity packages; installs only the
  ticked ones that are missing, shows installed ones.
- Settings: linear color space, Force Text, Visible Meta Files, Active Input Handling with restart button.
- `.gitignore` (+ extra lines), `.gitattributes` (binary or Git LFS), `git init`.
- Starter scenes with camera and light, added to the build list without duplicates.
- Choices saved in EditorPrefs and reused in the next project; opens once in projects without the root folder.
- Public `SetupSteps` API and `SetupReport`. 27 EditMode tests.
