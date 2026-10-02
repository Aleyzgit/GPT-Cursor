# Repository workflow

- Commit completed, validated changes in focused commits, as requested by the owner.
- Keep settings, build outputs, downloaded tools and test artifacts out of Git.
- Run the relevant checks before committing behavior changes.
- The release version comes from GPTCursor.csproj. Installer builds use that version.
- For a requested release, build the self-contained setup, publish a matching vMAJOR.MINOR.PATCH GitHub Release with the setup and SHA-256 file, and push the corresponding commits/tag.
- Never change Windows security policy, install trusted certificates, or inject into games to make the cursor visible.
