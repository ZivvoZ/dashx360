# Asset Audit

This project includes a mix of custom utility assets, fan-project placeholders, and media that may be derived from commercial properties.

## Lower-risk bundled assets

- `Assets/Profile/FriendPool/*`
- `Assets/Misc/SearchMenuIcons/*`
- `Assets/Misc/SettingsIcons/*`
- `Assets/Profile/profilepicture.jpg`

These are used as generic app UI/profile assets and are kept in place so the dashboard remains functional.

## Higher-risk assets that should be reviewed before a public GitHub push

- `Assets/Audio/Sounds/*`
- `Assets/Boot/Boot Screen.mp4`
- `Assets/Tiles/**/*`
- `Assets/GameCoverArt/**/*`
- `Assets/References/*`

These files may include copyrighted game art, movie art, music branding, dashboard references, or recreated platform media. They are still present in this public-ready working copy so the app continues to run, but they should be reviewed and replaced with safe alternatives if you want a cleaner public repository.

## Recommended replacement strategy

- Keep filenames and relative paths the same
- Swap in original placeholder art, neutral textures, or contributor-made assets
- Preserve dimensions where possible so tile layout does not need to change

## Personal/private audit result

- No personal screenshots, account tokens, or project-local log files are intentionally bundled in this public copy
- Launcher profile and settings data are stored outside the repository at runtime
