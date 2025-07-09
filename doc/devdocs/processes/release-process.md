# PowerToys Release Process

This document outlines the process for preparing and publishing PowerToys releases.

## Release Preparation

### Branch Management
1. Sync commits from main branch to stable branch
   - Usually sync current main to stable
   - For hotfixes: might need to cherry-pick specific commits

2. Start release build from the stable branch
   - Use pipelines to build
   - Set version number (e.g., 0.89.0)
   - Build for both x64 and ARM64
   - Build time: ~1-2 hours (signing can take extra time)
   - Build can be flaky, might need multiple attempts

3. Artifacts from the build:
   - ARM64 release files
     - PowerToys setup for ARM64 (machine setup)
     - User setup
   - X64 release files
     - PowerToys setup for x64 (machine setup)
     - User setup
   - GPO files (same for both architectures)
   - Hash files for verification
   - Symbols that are shipped with every release

### Versioning
- Uses semantic versioning: `MAJOR.MINOR.PATCH`
- MINOR version increases with regular releases (e.g., 0.89.0)
- PATCH version increases for hotfixes (e.g., 0.87.0 → 0.87.1)
- Each release version must be greater than the previous one for proper updating

## Testing Process

### Release Candidate Testing
1. Fully test the builds using a checklist
   - Manual tests for each release
   - Each test item should be verified by at least 2 people
   - Test on both x64 and ARM64 machines
   - Every module is tested by at least two people
   - New team members typically take 2 days for complete testing
   - Experienced team members complete testing in less than a day (~2 hours for 1/3 of tests)

2. For subsequent Release Candidates:
   - Full retesting of modules with changes
   - Verifying specific fixes
   - Sanity checking all utilities (ensuring no startup crashes)

3. If regressions found:
   - Fix issues
   - Return to step 1 (sync fixes to stable and build again)

### Testing Workflow
1. Team divides the test checklist among members
2. Each member performs assigned tests
3. Members report any issues found
4. Team assesses if issues are release blockers
5. Team confirms testing completion before proceeding

### Reporting Bugs During Testing
1. Discuss in team chat
2. Determine if it's a regression (check if bug exists in previous version)
3. Check if an issue is already open
4. Open a new issue if needed
5. Decide on criticality for the release (if regression)

### Sign-off Process
- Teams sign off on modules independently
- Regressions found in first release candidates lead to PRs
- Second release candidate verified fixes
- Final verification ensures modules don't crash with new features

## Documentation and Changelog

### README Updates
1. Create PR with README updates for the release:
   - Add new utilities to the list if applicable
   - Update milestones
   - Update expected download links
   - Upload new hashes
   - Update version and month
   - Write highlights of important changes
   - Thank open source contributors
     - Don't thank internal team members or Microsoft employees assigned to the project
     - Exception: thank external helpers like Niels (UI contributions)

### Changelog Creation
- Changelog PR should be created several days before release
- Community members need time to comment and request changes
- Project managers need time to review and clean up
- When team testing is set, either tests are done or changelog is created right away

### Changelog Structure
- **General section**: 
  - Issues/fixes not related to specific modules
  - User-visible changes
  - Important package updates (like .NET packages)
  - Fixes that affect end users

- **Development section**:
  - CI-related changes
  - Changes not visible to end users
  - Performance improvements internal to the system
  - Refactoring changes
  - Logger updates and other developer-focused improvements

### Formatting Notes
- Special attention needed for "highlights" section
- Different format is required for highlights in README versus release notes
- Must follow the exact same pattern/format for proper processing
- PowerToys pulls "What's New" information from the GitHub API
  - Gets changelog from the latest 5 releases
  - Format must be consistent for the PowerToys code to properly process it
  - Code behind will delete everything between certain markers (installer hashes and highlights)

### Documentation Changes
- Public docs appear on the web
- Changes happen in the Microsoft Docs repo: microsoft/windows-dev-docs
- For help with docs, contact Alvin Ashcraft from Microsoft
- Content automatically appears on learn.microsoft.com when PR is merged

## GitHub Release Process

### Creating the Release
1. Ask the project management team to start a GitHub release draft
   - Draft should target stable branch
   - Use proper version format (e.g., V 0.89.0)
   - Set title using same format (e.g., "Release V 0.89.0")

2. After testing is complete:
   - Pick up the hashes from artifacts
   - Apply changelog
   - Fill in release notes
   - Upload binaries
     - GPO files
     - Setup files
     - ZIP files with symbols
   - Only press "Save Draft", don't publish yet

3. Final verification:
   - Download every file from the draft
   - Check that ZIPs can be unzipped
   - Verify hashes match expectations
   - Tell the project management team the release is good to go
   - They will handle the actual publishing

### Post-Release Actions
- GitHub Actions automatically trigger:
  - Store submission
  - WinGet submission
- Monitor these actions to ensure they complete successfully
- If something fails, action may need to be taken

## Release Decision Making

### Timing Considerations
- Release owner should coordinate with project managers
- Project managers have high-level view of what should be included in the release
- Use the "in for .XX" tag to identify PRs that should be included
- If a key feature isn't ready, discuss with PMs whether to delay the release

### Release Coordination
- Release coordination requires good communication with domain feature owners
- Coordination needed with project managers and key feature developers
- Release candidate can only be done once key features have been merged
- Need to ensure all critical fixes are included before the release candidate

## Special Cases

### Hotfix Process
- For critical issues found after release
- Create a hotfix branch from the stable branch
- Cherry-pick only essential fixes
- Increment the PATCH version (e.g., 0.87.0 → 0.87.1)
- Follow the standard release process but with limited testing scope

### Community Testing
- Community members generally don't have access to draft builds
- Exception: Some Microsoft MVPs sometimes test ARM64 builds
- If providing builds to community members, use a different version number (e.g., 0.1.x) to avoid installer conflicts
