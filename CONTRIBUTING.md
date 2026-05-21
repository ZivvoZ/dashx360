# Contributing

Thanks for taking a look at the project.

## Ground rules

- Keep the Xbox 360-inspired visual language intact unless the change is clearly intentional and discussed first.
- Prefer small, reviewable pull requests over large refactors.
- Avoid committing personal data, private media, cached sessions, logs, or local-only configuration.
- If you add new art, audio, or branding-heavy assets, document where they came from and whether they can be redistributed.

## Development notes

- Target platform: Windows x64
- Framework: .NET 8 WPF
- The public release uses local-only social/profile data.

## Before opening a pull request

- Build the solution in Release mode
- Test controller navigation if your change touches input or the Guide
- Update docs when behavior changes
